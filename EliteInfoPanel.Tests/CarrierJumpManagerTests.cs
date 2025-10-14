using System;
using System.Threading;
using System.Windows.Threading;
using EliteInfoPanel.Core.Services;
using Xunit;

namespace EliteInfoPanel.Tests
{
    public class CarrierJumpManagerTests
    {
        private Dispatcher CreateDispatcher()
        {
            var thread = new Thread(() => Dispatcher.Run()) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            while (Dispatcher.FromThread(thread) == null) Thread.Sleep(10);
            return Dispatcher.FromThread(thread)!;
        }

        [Fact]
        public void Schedule_and_complete_jump_raises_events()
        {
            var dispatcher = CreateDispatcher();
            var manager = new CarrierJumpManager(dispatcher, () => true, () => { });

            bool scheduledRaised = false;
            bool completedRaised = false;
            manager.CarrierJumpScheduled += (_, __) => scheduledRaised = true;
            manager.CarrierJumpCompleted += (_, __) => completedRaised = true;

            var t = DateTime.UtcNow.AddSeconds(3);
            manager.ScheduleJump(t, "Sol", "A1");
            Assert.True(manager.IsJumpScheduled);
            Assert.Equal("Sol", manager.DestinationSystem);

            manager.CompleteJump();
            Assert.True(manager.JumpCompleted);
            Assert.True(scheduledRaised);
            Assert.True(completedRaised);
        }
    }
}
