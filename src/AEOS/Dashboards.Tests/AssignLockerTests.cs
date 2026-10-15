using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Tests
{
    public class AssignLockerTests
    {
        private const long CourierLocker = 204;   // in "Courier Items", a Locker Management group
        private const long PersonalLocker = 301;  // in "All Personal", not managed by Locker Management
        private const long Employee = 77;
        private const int NetworkId = 3;
        private const long PresetId = 9;
        private static readonly DateTime HeldSince = new DateTime(2026, 9, 2);

        private readonly IDataOrchestrator orchestrator = Substitute.For<IDataOrchestrator>();
        private readonly IAeosDataAdapter aeos = Substitute.For<IAeosDataAdapter>();
        private readonly LockerAnalyticsApiController controller;

        public AssignLockerTests()
        {
            var analytics = new LockerAnalytics();
            analytics.Groups.Add(Group("Courier Items", CourierLocker, "M-204"));
            analytics.Groups.Add(Group("All Personal", PersonalLocker, "1N-301"));
            orchestrator.GetLockerAnalytics().Returns(analytics);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AeosDashboards:AllowChanges"] = "true",
                    ["AeosDashboards:LockerManagement:GroupConfiguration:0:groupName"] = "Courier Items",
                })
                .Build();

            controller = new LockerAnalyticsApiController(orchestrator, configuration, aeos);
        }

        private static LockerGroupAnalytics Group(string name, long lockerId, string lockerName) => new LockerGroupAnalytics
        {
            Name = name,
            LockerAuthorisationGroupNetworkId = NetworkId,
            LockerAuthorisationPresetId = PresetId,
            AllLockers = new List<LockerInfo> { new LockerInfo { Id = lockerId, Name = lockerName } },
        };

        private static LiveLockerState Free(long id) => new LiveLockerState(id, null, null);
        private static LiveLockerState Held(long id) => new LiveLockerState(id, null, HeldSince);
        private static LiveLockerState AssignedTo(long id, long carrier) => new LiveLockerState(id, carrier, HeldSince);

        private Task<IActionResult> Assign(long lockerId, bool releaseIfHeld = false) =>
            controller.AssignLocker(new AssignLockerRequest { EmployeeId = Employee, LockerId = lockerId, ReleaseIfHeld = releaseIfHeld });

        private static (int Status, JsonElement Body) Read(IActionResult result)
        {
            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            return (objectResult.StatusCode ?? 200, JsonSerializer.SerializeToElement(objectResult.Value));
        }

        private static string? Code(JsonElement body) =>
            body.TryGetProperty("code", out var code) ? code.GetString() : null;

        private void AssignFails(long lockerId, string aeosMessage = "Failed to assign locker: Add locker authorizations failed") =>
            aeos.AssignLocker(lockerId, Employee, NetworkId, PresetId).ThrowsAsync(new Exception(aeosMessage));

        // --- Plain assignment (no ReleaseIfHeld): the happy path is unchanged ---

        [Fact]
        public async Task Successful_assignment_does_not_read_the_live_state_or_release()
        {
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(200, status);
            Assert.True(body.GetProperty("success").GetBoolean());
            Assert.False(body.GetProperty("releasedHeldLocker").GetBoolean());
            await aeos.DidNotReceiveWithAnyArgs().GetLiveLockerState(default);
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Successful_assignment_stays_successful_when_the_refresh_afterwards_fails()
        {
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);
            orchestrator.GetLockersData().ThrowsAsync(new Exception("AEOS timeout"));

            var (status, _) = Read(await Assign(CourierLocker));

            Assert.Equal(200, status);
        }

        [Fact]
        public async Task Rejected_assignment_of_a_held_locker_answers_LOCKER_HELD_without_releasing()
        {
            AssignFails(CourierLocker);
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_HELD", Code(body));
            Assert.Equal(HeldSince, body.GetProperty("lastUsed").GetDateTime());
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Held_locker_outside_Locker_Management_groups_keeps_the_original_error()
        {
            AssignFails(PersonalLocker);
            aeos.GetLiveLockerState(PersonalLocker).Returns(Held(PersonalLocker));

            var (status, body) = Read(await Assign(PersonalLocker));

            Assert.Equal(500, status);
            Assert.Null(Code(body));
            Assert.Equal("Failed to assign locker: Add locker authorizations failed", body.GetProperty("details").GetString());
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Rejected_assignment_of_an_already_assigned_locker_answers_LOCKER_ALREADY_ASSIGNED()
        {
            AssignFails(CourierLocker);
            aeos.GetLiveLockerState(CourierLocker).Returns(AssignedTo(CourierLocker, 55));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_ALREADY_ASSIGNED", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Rejected_assignment_of_a_free_locker_keeps_the_original_error()
        {
            AssignFails(CourierLocker, "Failed to assign locker: something else");
            aeos.GetLiveLockerState(CourierLocker).Returns(Free(CourierLocker));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(500, status);
            Assert.Null(Code(body));
            Assert.Equal("Failed to assign locker: something else", body.GetProperty("details").GetString());
        }

        [Fact]
        public async Task Rejected_assignment_keeps_the_original_error_when_the_live_read_fails()
        {
            AssignFails(CourierLocker);
            aeos.GetLiveLockerState(CourierLocker).ThrowsAsync(new Exception("AEOS unreachable"));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(500, status);
            Assert.Equal("Failed to assign locker: Add locker authorizations failed", body.GetProperty("details").GetString());
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Assignment_that_returns_false_keeps_the_original_error_for_a_free_locker()
        {
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(false);
            aeos.GetLiveLockerState(CourierLocker).Returns(Free(CourierLocker));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(500, status);
            Assert.Equal("Failed to assign locker.", body.GetProperty("error").GetString());
        }

        // --- Confirmed "Release & assign" (ReleaseIfHeld) ---

        [Fact]
        public async Task Release_and_assign_releases_a_held_locker_verifies_it_and_then_assigns()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), Free(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(true);
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(200, status);
            Assert.True(body.GetProperty("releasedHeldLocker").GetBoolean());
            Received.InOrder(() =>
            {
                aeos.GetLiveLockerState(CourierLocker);
                aeos.ReleaseLocker(CourierLocker);
                aeos.GetLiveLockerState(CourierLocker);
                aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId);
            });
        }

        [Fact]
        public async Task Release_and_assign_on_a_free_locker_assigns_without_releasing()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Free(CourierLocker));
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(200, status);
            Assert.False(body.GetProperty("releasedHeldLocker").GetBoolean());
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
        }

        [Fact]
        public async Task Release_and_assign_never_releases_a_locker_that_is_assigned_meanwhile()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(AssignedTo(CourierLocker, 55));

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_ALREADY_ASSIGNED", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_stops_when_the_live_state_cannot_be_read()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns((LiveLockerState?)null);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(500, status);
            Assert.Equal("LOCKER_STATE_UNKNOWN", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_stops_when_the_live_read_throws()
        {
            aeos.GetLiveLockerState(CourierLocker).ThrowsAsync(new Exception("AEOS unreachable"));

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(500, status);
            Assert.Equal("LOCKER_STATE_UNKNOWN", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_does_not_assign_when_AEOS_refuses_the_release()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(false);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_RELEASE_REJECTED", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_does_not_assign_when_the_release_throws()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).ThrowsAsync(new Exception("timeout"));

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(500, status);
            Assert.Equal("LOCKER_RELEASE_FAILED", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_does_not_assign_when_the_locker_is_still_held_after_release()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), Held(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_STILL_HELD", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_does_not_assign_when_the_release_cannot_be_verified()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Task.FromResult<LiveLockerState?>(Held(CourierLocker)), Task.FromException<LiveLockerState?>(new Exception("AEOS unreachable")));
            aeos.ReleaseLocker(CourierLocker).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_STILL_HELD", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_reports_the_release_when_the_assignment_still_fails()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), Free(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(true);
            AssignFails(CourierLocker);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(500, status);
            Assert.True(body.GetProperty("releasedHeldLocker").GetBoolean());
            Assert.Equal("Failed to assign locker: Add locker authorizations failed", body.GetProperty("details").GetString());
        }

        [Fact]
        public async Task Release_and_assign_is_refused_outside_Locker_Management_groups()
        {
            var (status, _) = Read(await Assign(PersonalLocker, releaseIfHeld: true));

            Assert.Equal(400, status);
            await aeos.DidNotReceiveWithAnyArgs().GetLiveLockerState(default);
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }
    }
}
