using System;
using Innovatrics.SmartFace.Integrations.AeosDashboards;
using Xunit;
using AeosLocker = ServiceReference.LockerInfo;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Tests
{
    public class LiveLockerStateTests
    {
        private static readonly DateTime SeptemberSecond = new DateTime(2026, 9, 2);

        private static AeosLocker Locker(long id, long? assignedTo = null, DateTime? lastUsed = null)
        {
            var locker = new AeosLocker { Id = id, IdSpecified = true };
            if (assignedTo.HasValue)
            {
                locker.AssignedTo = assignedTo.Value;
                locker.AssignedToSpecified = true;
            }
            if (lastUsed.HasValue)
            {
                locker.LastUsed = lastUsed.Value;
                locker.LastUsedSpecified = true;
            }
            return locker;
        }

        [Fact]
        public void Free_locker_without_AssignedTo_and_LastUsed_is_neither_assigned_nor_held()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204) }, 204);

            Assert.NotNull(state);
            Assert.Equal(204, state!.Id);
            Assert.Null(state.AssignedTo);
            Assert.Null(state.LastUsed);
            Assert.False(state.IsAssigned);
            Assert.False(state.IsHeldWithoutOwner);
        }

        [Fact]
        public void Locker_with_LastUsed_and_no_carrier_is_held_without_owner()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204, lastUsed: SeptemberSecond) }, 204);

            Assert.NotNull(state);
            Assert.True(state!.IsHeldWithoutOwner);
            Assert.False(state.IsAssigned);
            Assert.Equal(SeptemberSecond, state.LastUsed);
        }

        [Fact]
        public void Assigned_locker_is_assigned_and_not_held_without_owner()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204, assignedTo: 42, lastUsed: SeptemberSecond) }, 204);

            Assert.NotNull(state);
            Assert.True(state!.IsAssigned);
            Assert.Equal(42, state.AssignedTo);
            Assert.False(state.IsHeldWithoutOwner);
        }

        [Fact]
        public void AssignedTo_zero_counts_as_no_carrier()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204, assignedTo: 0, lastUsed: SeptemberSecond) }, 204);

            Assert.NotNull(state);
            Assert.False(state!.IsAssigned);
            Assert.True(state.IsHeldWithoutOwner);
        }

        [Fact]
        public void LastUsed_flagged_as_present_but_MinValue_counts_as_no_LastUsed()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204, lastUsed: DateTime.MinValue) }, 204);

            Assert.NotNull(state);
            Assert.Null(state!.LastUsed);
            Assert.False(state.IsHeldWithoutOwner);
        }

        [Fact]
        public void LastUsed_value_without_its_Specified_flag_is_ignored()
        {
            var locker = Locker(204);
            locker.LastUsed = SeptemberSecond; // LastUsedSpecified stays false: AEOS did not send it

            var state = LiveLockerState.FromAeos(new[] { locker }, 204);

            Assert.NotNull(state);
            Assert.Null(state!.LastUsed);
            Assert.False(state.IsHeldWithoutOwner);
        }

        [Fact]
        public void Result_without_the_requested_id_gives_no_state()
        {
            // A search that AEOS did not filter by id returns other lockers.
            var state = LiveLockerState.FromAeos(new[] { Locker(101, lastUsed: SeptemberSecond), Locker(102) }, 204);

            Assert.Null(state);
        }

        [Fact]
        public void Result_with_the_requested_id_twice_gives_no_state()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(204), Locker(204, lastUsed: SeptemberSecond) }, 204);

            Assert.Null(state);
        }

        [Fact]
        public void Missing_or_empty_result_gives_no_state()
        {
            Assert.Null(LiveLockerState.FromAeos(null, 204));
            Assert.Null(LiveLockerState.FromAeos(Array.Empty<AeosLocker>(), 204));
            Assert.Null(LiveLockerState.FromAeos(new AeosLocker?[] { null }, 204));
        }

        [Fact]
        public void The_requested_locker_is_picked_out_of_a_longer_result()
        {
            var state = LiveLockerState.FromAeos(new[] { Locker(101), Locker(204, lastUsed: SeptemberSecond) }, 204);

            Assert.NotNull(state);
            Assert.Equal(204, state!.Id);
            Assert.True(state.IsHeldWithoutOwner);
        }
    }
}
