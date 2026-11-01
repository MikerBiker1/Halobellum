// ItemDragTests.cs
// Dragging an item from the inventory to a hexagon (decision D35): releasing over a hexagon places it,
// releasing elsewhere after dragging cancels, and a plain click selects the item (a second click on it
// deselects), so a hexagon can also be clicked afterwards.
// Needs Unity's Vector2, so it runs only in Unity's Test Runner.
// Depends on: Halobellum.UI (ItemDrag), Halobellum.Core (ItemStats), NUnit, UnityEngine.

using Halobellum.Core;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class ItemDragTests
    {
        private static readonly ItemStats Item = new ItemStats { Name = "Heavy Armor" };

        [Test]
        public void ReleasedOverAHexagon_AfterDragging_PlacesTheItem()
        {
            var drag = new ItemDrag();
            drag.Press(Item, new Vector2(50f, 300f), alreadySelected: false);
            Assert.IsTrue(drag.IsDragging);
            Assert.AreEqual(ItemDrag.Outcome.Place, drag.Release(new Vector2(900f, 700f), overHex: true));
            Assert.IsFalse(drag.IsDragging);
        }

        [Test]
        public void ReleasedAwayFromAHexagon_AfterDragging_Cancels()
        {
            var drag = new ItemDrag();
            drag.Press(Item, new Vector2(50f, 300f), alreadySelected: false);
            Assert.AreEqual(ItemDrag.Outcome.Cancel, drag.Release(new Vector2(900f, 700f), overHex: false));
        }

        [Test]
        public void APlainClick_SelectsTheItem_ASecondClickDeselects()
        {
            var drag = new ItemDrag();
            drag.Press(Item, new Vector2(50f, 300f), alreadySelected: false);
            Assert.AreEqual(ItemDrag.Outcome.KeepSelected, drag.Release(new Vector2(52f, 301f), overHex: false));
            drag.Press(Item, new Vector2(50f, 300f), alreadySelected: true);
            Assert.AreEqual(ItemDrag.Outcome.Cancel, drag.Release(new Vector2(50f, 300f), overHex: false));
        }

        [Test]
        public void APlainClick_NeverPlaces_EvenIfAHexagonIsBehindThePanel()
        {
            var drag = new ItemDrag();
            drag.Press(Item, new Vector2(50f, 300f), alreadySelected: false);
            Assert.AreEqual(ItemDrag.Outcome.KeepSelected, drag.Release(new Vector2(50f, 300f), overHex: true));
        }

        [Test]
        public void ReleaseWithoutAPress_DoesNothing()
        {
            Assert.AreEqual(ItemDrag.Outcome.None, new ItemDrag().Release(Vector2.zero, overHex: true));
        }
    }
}
