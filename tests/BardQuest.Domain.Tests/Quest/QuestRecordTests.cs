using BardQuest.Domain.Progression;
using BardQuest.Domain.Quest;

using Xunit;

using YARG.Core;

namespace BardQuest.Domain.Tests.Quest
{
    public class QuestRecordTests
    {
        [Fact]
        public void QuestCarriesItsPersistedState()
        {
            var link = new ProvenanceLink(42, "abc123", new DateTime(2026, 7, 11));
            var delivery = new DeliveryState(RerunCount: 0, WorkingSet: ["h1", "h2"], BossHash: null);
            var quest = new BardQuest.Domain.Quest.Quest(
                Guid.NewGuid(), Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert,
                QuestPace.Journey, new DateTime(2026, 7, 11), [link], delivery);

            Assert.Equal(Instrument.ProDrums, quest.Instrument);
            Assert.Equal(Difficulty.Expert, quest.Difficulty);
            Assert.Single(quest.Links);
            Assert.Equal(42, quest.Links[0].PlayerScoreRecordId);
            Assert.Equal(2, quest.Delivery.WorkingSet.Count);
        }

        [Fact]
        public void AppendingALinkIsANonMutatingWith()
        {
            var quest = new BardQuest.Domain.Quest.Quest(
                Guid.NewGuid(), Guid.NewGuid(), Instrument.ProDrums, Difficulty.Expert,
                QuestPace.Journey, DateTime.UtcNow, [], new DeliveryState(0, [], null));

            var appended = quest with { Links = [new ProvenanceLink(1, "h", DateTime.UtcNow)] };

            Assert.Empty(quest.Links);       // original unchanged
            Assert.Single(appended.Links);
        }
    }
}
