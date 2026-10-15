using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class AddTypeMenuAttributeTests
    {
        [Test]
        public void MenuPath_ExposesSegmentsAndLeafName ()
        {
            var attribute = new AddTypeMenuAttribute("Commands/Movement/Run", -4);

            Assert.That(attribute.GetSplittedMenuName(), Is.EqualTo(new[] { "Commands", "Movement", "Run" }));
            Assert.That(attribute.GetTypeNameWithoutPath(), Is.EqualTo("Run"));
            Assert.That(attribute.MenuName, Is.EqualTo("Commands/Movement/Run"));
            Assert.That(attribute.Order, Is.EqualTo(-4));
        }
    }
}
