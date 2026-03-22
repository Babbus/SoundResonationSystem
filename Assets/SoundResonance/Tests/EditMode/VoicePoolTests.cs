using NUnit.Framework;

namespace SoundResonance.Tests
{
    /// <summary>
    /// Validates VoicePool slot assignment, stealing, and release logic.
    /// Uses pool size of 4 for test simplicity.
    /// </summary>
    public class VoicePoolTests
    {
        private VoicePool _pool;
        private const int TestPoolSize = 4;

        [SetUp]
        public void SetUp()
        {
            _pool = new VoicePool(TestPoolSize);
        }

        [Test]
        public void AssignVoice_NewEntity_ReturnsValidSlotIndex()
        {
            int slot = _pool.GetOrAssign(entityId: 1, currentAmplitude: 0.5f);
            Assert.That(slot, Is.InRange(0, TestPoolSize - 1),
                "Slot index should be within pool bounds");
        }

        [Test]
        public void AssignVoice_SameEntityTwice_ReturnsSameSlot()
        {
            int slot1 = _pool.GetOrAssign(entityId: 42, currentAmplitude: 0.5f);
            int slot2 = _pool.GetOrAssign(entityId: 42, currentAmplitude: 0.5f);
            Assert.AreEqual(slot1, slot2,
                "Same entity should always get the same slot");
        }

        [Test]
        public void AssignVoice_PoolFull_StealsLowestAmplitudeSlot()
        {
            // Fill pool with 4 entities at different amplitudes
            _pool.GetOrAssign(entityId: 1, currentAmplitude: 0.8f);
            _pool.UpdateAmplitude(_pool.GetAssignedSlot(1), 0.8f);

            _pool.GetOrAssign(entityId: 2, currentAmplitude: 0.1f); // lowest
            _pool.UpdateAmplitude(_pool.GetAssignedSlot(2), 0.1f);

            _pool.GetOrAssign(entityId: 3, currentAmplitude: 0.5f);
            _pool.UpdateAmplitude(_pool.GetAssignedSlot(3), 0.5f);

            _pool.GetOrAssign(entityId: 4, currentAmplitude: 0.9f);
            _pool.UpdateAmplitude(_pool.GetAssignedSlot(4), 0.9f);

            // Pool is now full. Entity 2 has lowest amplitude (0.1).
            int stolenSlot = _pool.GetAssignedSlot(2);

            // Assign a 5th entity -- should steal entity 2's slot
            int newSlot = _pool.GetOrAssign(entityId: 5, currentAmplitude: 0.6f);

            Assert.AreEqual(stolenSlot, newSlot,
                "New entity should steal the slot of the lowest amplitude entity");
            Assert.AreEqual(-1, _pool.GetAssignedSlot(2),
                "Stolen entity should no longer have an assigned slot");
        }

        [Test]
        public void ReleaseVoice_FreesSlotForReuse()
        {
            int slot = _pool.GetOrAssign(entityId: 10, currentAmplitude: 0.5f);
            Assert.IsTrue(_pool.IsSlotActive(slot), "Slot should be active after assignment");

            _pool.ReleaseVoice(slot);
            Assert.IsFalse(_pool.IsSlotActive(slot), "Slot should be inactive after release");

            // New entity should be able to use the freed slot
            int newSlot = _pool.GetOrAssign(entityId: 20, currentAmplitude: 0.5f);
            Assert.That(newSlot, Is.InRange(0, TestPoolSize - 1),
                "New entity should get a valid slot after release");
        }

        [Test]
        public void AfterRelease_SameEntity_GetsNewSlot()
        {
            int slot1 = _pool.GetOrAssign(entityId: 7, currentAmplitude: 0.5f);
            _pool.ReleaseVoice(slot1);

            // Re-assign same entity -- should get a fresh slot (may or may not be same index)
            int slot2 = _pool.GetOrAssign(entityId: 7, currentAmplitude: 0.5f);
            Assert.That(slot2, Is.InRange(0, TestPoolSize - 1),
                "Re-assigned entity should get a valid slot");
            Assert.IsTrue(_pool.IsSlotActive(slot2),
                "Re-assigned slot should be active");
        }

        [Test]
        public void GetAssignedSlot_UnassignedEntity_ReturnsNegativeOne()
        {
            int slot = _pool.GetAssignedSlot(entityId: 999);
            Assert.AreEqual(-1, slot,
                "Unassigned entity should return -1");
        }

        [Test]
        public void UpdateAmplitude_AffectsStealingPriority()
        {
            // Fill pool
            _pool.GetOrAssign(entityId: 1, currentAmplitude: 0.5f);
            _pool.GetOrAssign(entityId: 2, currentAmplitude: 0.5f);
            _pool.GetOrAssign(entityId: 3, currentAmplitude: 0.5f);
            _pool.GetOrAssign(entityId: 4, currentAmplitude: 0.5f);

            // Update entity 3 to have the lowest amplitude
            _pool.UpdateAmplitude(_pool.GetAssignedSlot(3), 0.01f);

            int entity3Slot = _pool.GetAssignedSlot(3);

            // New entity should steal entity 3's slot (lowest amplitude)
            int newSlot = _pool.GetOrAssign(entityId: 5, currentAmplitude: 0.5f);
            Assert.AreEqual(entity3Slot, newSlot,
                "Should steal the slot with lowest updated amplitude");
        }

        [Test]
        public void MultipleEntities_GetDifferentSlots()
        {
            int slot1 = _pool.GetOrAssign(entityId: 100, currentAmplitude: 0.5f);
            int slot2 = _pool.GetOrAssign(entityId: 200, currentAmplitude: 0.5f);
            int slot3 = _pool.GetOrAssign(entityId: 300, currentAmplitude: 0.5f);

            Assert.AreNotEqual(slot1, slot2, "Different entities should get different slots");
            Assert.AreNotEqual(slot2, slot3, "Different entities should get different slots");
            Assert.AreNotEqual(slot1, slot3, "Different entities should get different slots");
        }
    }
}
