using System.IO;
using System.Xml.Linq;
using Xunit;

namespace InfoLoomBridge.Tests
{
    public sealed class ProjectMetadataTests
    {
        [Fact]
        public void Main_project_declares_publish_metadata()
        {
            string projectPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "InfoLoomBridge.csproj"));
            XDocument project = XDocument.Load(projectPath);
            XElement propertyGroup = project.Root!.Element("PropertyGroup")!;

            Assert.Equal("InfoLoom Bridge", propertyGroup.Element("DisplayName")?.Value);
            Assert.Equal(
                "Exports selected InfoLoom data to a local JSON snapshot for external tools.",
                propertyGroup.Element("ShortDescription")?.Value);
            Assert.False(string.IsNullOrWhiteSpace(propertyGroup.Element("GameVersion")?.Value));
            Assert.Equal("Properties\\PublishConfiguration.xml", propertyGroup.Element("PublishConfigurationPath")?.Value);
        }

        [Fact]
        public void Publish_configuration_matches_project_metadata()
        {
            string projectPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "InfoLoomBridge.csproj"));
            string publishPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Properties", "PublishConfiguration.xml"));

            XDocument project = XDocument.Load(projectPath);
            XDocument publish = XDocument.Load(publishPath);
            XElement propertyGroup = project.Root!.Element("PropertyGroup")!;
            XElement publishRoot = publish.Root!;

            Assert.Equal(propertyGroup.Element("DisplayName")?.Value, publishRoot.Element("DisplayName")?.Value);
            Assert.Equal(propertyGroup.Element("ShortDescription")?.Value, publishRoot.Element("ShortDescription")?.Value);
            Assert.Equal(propertyGroup.Element("Version")?.Value, publishRoot.Element("Version")?.Value);
            Assert.Equal(propertyGroup.Element("GameVersion")?.Value, publishRoot.Element("GameVersion")?.Value);
        }
    }
}
