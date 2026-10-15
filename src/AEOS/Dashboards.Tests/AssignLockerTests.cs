using System;
using System.Collections.Generic;
using System.Linq;
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
        private const long OtherEmployee = 88;
        private const int NetworkId = 3;
        private const long PresetId = 9;
        private static readonly DateTime HeldSince = new DateTime(2026, 9, 2);

        private readonly IDataOrchestrator orchestrator = Substitute.For<IDataOrchestrator>();
        private readonly IAeosDataAdapter aeos = Substitute.For<IAeosDataAdapter>();
        private readonly LockerAnalyticsApiController controller;
        private readonly IConfiguration configuration;

        public AssignLockerTests()
        {
            var analytics = new LockerAnalytics();
            analytics.Groups.Add(Group("Courier Items", CourierLocker, "M-204"));
            analytics.Groups.Add(Group("All Personal", PersonalLocker, "1N-301"));
            orchestrator.GetLockerAnalytics().Returns(analytics);

            configuration = new ConfigurationBuilder()
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

        // A confirmed retry carries the hold date it confirmed, as the page sends it.
        private Task<IActionResult> Assign(long lockerId, bool releaseIfHeld = false, DateTime? heldSince = null) =>
            controller.AssignLocker(new AssignLockerRequest { EmployeeId = Employee, LockerId = lockerId, ReleaseIfHeld = releaseIfHeld, HeldSince = heldSince ?? (releaseIfHeld ? HeldSince : null) });

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
        public async Task Rejected_assignment_keeps_the_original_error_when_AEOS_does_not_return_the_locker()
        {
            AssignFails(CourierLocker);
            aeos.GetLiveLockerState(CourierLocker).Returns((LiveLockerState?)null);

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(500, status);
            Assert.Null(Code(body));
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
            Assert.Equal("The locker was released in AEOS, but AEOS did not confirm the assignment.", body.GetProperty("error").GetString());
            Assert.Equal("Refresh the page to see whether the locker is assigned before trying again.", body.GetProperty("message").GetString());
            Assert.Equal("Failed to assign locker: Add locker authorizations failed", body.GetProperty("details").GetString());
        }

        [Fact]
        public async Task Release_and_assign_does_not_assign_when_someone_else_got_the_locker_right_after_the_release()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), AssignedTo(CourierLocker, 55));
            aeos.ReleaseLocker(CourierLocker).Returns(true);

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_STILL_HELD", Code(body));
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Release_and_assign_releases_when_the_hold_is_the_one_the_receptionist_confirmed()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), Free(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(true);
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var (status, _) = Read(await Assign(CourierLocker, releaseIfHeld: true, heldSince: HeldSince));

            Assert.Equal(200, status);
            await aeos.Received(1).ReleaseLocker(CourierLocker);
        }

        [Fact]
        public async Task Release_and_assign_does_not_release_a_hold_that_started_after_the_confirmation()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker));

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true, heldSince: HeldSince.AddDays(-5)));

            Assert.Equal(409, status);
            Assert.Equal("LOCKER_HOLD_CHANGED", Code(body));
            Assert.Equal(HeldSince, body.GetProperty("lastUsed").GetDateTime());
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
        }

        [Fact]
        public async Task Unconfirmed_assignment_that_reads_back_as_done_is_a_success()
        {
            AssignFails(CourierLocker, "The request channel timed out");
            aeos.GetLiveLockerState(CourierLocker).Returns(AssignedTo(CourierLocker, Employee));

            var (status, body) = Read(await Assign(CourierLocker));

            Assert.Equal(200, status);
            Assert.True(body.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task Unconfirmed_assignment_after_a_release_that_reads_back_as_done_is_a_success()
        {
            aeos.GetLiveLockerState(CourierLocker).Returns(Held(CourierLocker), Free(CourierLocker), AssignedTo(CourierLocker, Employee));
            aeos.ReleaseLocker(CourierLocker).Returns(true);
            AssignFails(CourierLocker, "The request channel timed out");

            var (status, body) = Read(await Assign(CourierLocker, releaseIfHeld: true));

            Assert.Equal(200, status);
            Assert.True(body.GetProperty("releasedHeldLocker").GetBoolean());
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Utc)]
        public async Task LOCKER_HELD_lastUsed_sent_back_by_the_page_as_heldSince_lets_the_release_through(DateTimeKind kind)
        {
            // ASP.NET Core serialises with the web defaults; the page posts data.lastUsed back unchanged.
            var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var heldSince = DateTime.SpecifyKind(HeldSince, kind);
            AssignFails(CourierLocker);
            aeos.GetLiveLockerState(CourierLocker).Returns(new LiveLockerState(CourierLocker, null, heldSince));

            var heldAnswer = JsonSerializer.Serialize(((ObjectResult)await Assign(CourierLocker)).Value, web);
            var lastUsed = JsonDocument.Parse(heldAnswer).RootElement.GetProperty("lastUsed").GetString();
            var pageBody = "{\"employeeId\":" + Employee + ",\"lockerId\":" + CourierLocker + ",\"lockerAuthorisationGroupNetworkId\":" + NetworkId
                + ",\"lockerAuthorisationPresetId\":" + PresetId + ",\"releaseIfHeld\":true,\"heldSince\":\"" + lastUsed + "\"}";
            var request = JsonSerializer.Deserialize<AssignLockerRequest>(pageBody, web)!;

            aeos.ClearReceivedCalls();
            aeos.GetLiveLockerState(CourierLocker).Returns(new LiveLockerState(CourierLocker, null, heldSince), Free(CourierLocker));
            aeos.ReleaseLocker(CourierLocker).Returns(true);
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var (status, _) = Read(await controller.AssignLocker(request));

            Assert.True(request.ReleaseIfHeld);
            Assert.Equal(HeldSince.Date, request.HeldSince?.Date); // the date check really ran
            Assert.Equal(200, status);
            await aeos.Received(1).ReleaseLocker(CourierLocker);
        }

        [Fact]
        public void Plain_assign_body_from_the_page_binds_without_release()
        {
            var body = "{\"employeeId\":77,\"lockerId\":204,\"lockerAuthorisationGroupNetworkId\":3,\"lockerAuthorisationPresetId\":9,\"releaseIfHeld\":false,\"heldSince\":null}";

            var request = JsonSerializer.Deserialize<AssignLockerRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

            Assert.False(request.ReleaseIfHeld);
            Assert.Null(request.HeldSince);
            Assert.Equal(204, request.LockerId);
            Assert.Equal(77, request.EmployeeId);
        }

        [Fact]
        public async Task Concurrent_release_and_assign_on_one_locker_releases_it_only_once()
        {
            // A reads the locker as held and releases it; B arrives while A's release is in flight.
            // B must wait for A, then see A's assignment and leave it alone.
            var releaseInFlight = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = 0;
            aeos.GetLiveLockerState(CourierLocker).Returns(_ =>
            {
                reads++;
                return reads switch
                {
                    1 => Held(CourierLocker),              // A, before releasing
                    2 => Free(CourierLocker),              // A, verifying its release
                    _ => AssignedTo(CourierLocker, Employee) // B, after A assigned
                };
            });
            aeos.ReleaseLocker(CourierLocker).Returns(releaseInFlight.Task);
            aeos.AssignLocker(CourierLocker, Employee, NetworkId, PresetId).Returns(true);

            var first = Assign(CourierLocker, releaseIfHeld: true);
            Task<IActionResult>? second = null;
            int readsWhileFirstHeldTheLock;
            try
            {
                await WaitUntil(() => aeos.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IAeosDataAdapter.ReleaseLocker)));

                // Another receptionist assigns the same locker to someone else. Each HTTP request gets its
                // own controller instance; the lock must hold across them and must be per locker.
                var otherController = new LockerAnalyticsApiController(orchestrator, configuration, aeos);
                second = otherController.AssignLocker(new AssignLockerRequest { EmployeeId = OtherEmployee, LockerId = CourierLocker, ReleaseIfHeld = true, HeldSince = HeldSince });
                await Task.Delay(200);
                readsWhileFirstHeldTheLock = reads;
            }
            finally
            {
                // Always let A finish: a failure above must not leave the locker's lock held for other tests.
                releaseInFlight.TrySetResult(true);
            }

            var (firstStatus, _) = Read(await first);
            var (secondStatus, secondBody) = Read(await second!);

            Assert.Equal(1, readsWhileFirstHeldTheLock); // B did not read the locker while A held it
            Assert.Equal(200, firstStatus);
            Assert.Equal(409, secondStatus);
            Assert.Equal("LOCKER_ALREADY_ASSIGNED", Code(secondBody));
            await aeos.Received(1).ReleaseLocker(CourierLocker);
            await aeos.Received(1).AssignLocker(CourierLocker, Employee, NetworkId, PresetId);
            await aeos.DidNotReceive().AssignLocker(CourierLocker, OtherEmployee, NetworkId, PresetId);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var i = 0; i < 500 && !condition(); i++)
            {
                await Task.Delay(10);
            }
            Assert.True(condition(), "condition not reached");
        }

        [Fact]
        public async Task Release_and_assign_without_heldSince_is_refused_before_touching_AEOS()
        {
            var request = new AssignLockerRequest { EmployeeId = Employee, LockerId = CourierLocker, ReleaseIfHeld = true, HeldSince = null };

            var (status, _) = Read(await controller.AssignLocker(request));

            Assert.Equal(400, status);
            await aeos.DidNotReceiveWithAnyArgs().GetLiveLockerState(default);
            await aeos.DidNotReceiveWithAnyArgs().ReleaseLocker(default);
            await aeos.DidNotReceiveWithAnyArgs().AssignLocker(default, default, default, default);
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
