using MortysDLP.Helpers;
using System.Collections.Generic;

namespace MortysDLP.Tests
{
    public class ActiveWorkHelperTests
    {
        private sealed class FakeWork(bool isBusy, string label) : ICancellableWork
        {
            public bool IsBusy { get; } = isBusy;
            public string BusyLabel { get; } = label;
            public int CancelCalls { get; private set; }
            public void RequestCancel() => CancelCalls++;
        }

        [Fact]
        public void FindBusy_KeineQuellen_LiefertLeereListe()
        {
            var result = ActiveWorkHelper.FindBusy([]);

            Assert.Empty(result);
        }

        [Fact]
        public void FindBusy_NichtsAktiv_LiefertLeereListe()
        {
            var sources = new List<ICancellableWork> { new FakeWork(false, "Download"), new FakeWork(false, "Konvertierung") };

            var result = ActiveWorkHelper.FindBusy(sources);

            Assert.Empty(result);
        }

        [Fact]
        public void FindBusy_EinigeAktiv_LiefertNurDieseInReihenfolge()
        {
            var idle = new FakeWork(false, "Download");
            var busy1 = new FakeWork(true, "Konvertierung");
            var busy2 = new FakeWork(true, "Transkription");
            var sources = new List<ICancellableWork> { idle, busy1, busy2 };

            var result = ActiveWorkHelper.FindBusy(sources);

            Assert.Equal([busy1, busy2], result);
        }

        [Fact]
        public void FindBusy_RuftRequestCancelNichtSelbstAuf()
        {
            var busy = new FakeWork(true, "Download");

            ActiveWorkHelper.FindBusy([busy]);

            Assert.Equal(0, busy.CancelCalls);
        }

        [Fact]
        public void RequestCancel_AufGefundeneQuellenAngewendet_BrichtGenauDieseAb()
        {
            var idle = new FakeWork(false, "Download");
            var busy = new FakeWork(true, "Twitch-Download");
            var sources = new List<ICancellableWork> { idle, busy };

            foreach (var work in ActiveWorkHelper.FindBusy(sources))
                work.RequestCancel();

            Assert.Equal(0, idle.CancelCalls);
            Assert.Equal(1, busy.CancelCalls);
        }
    }
}
