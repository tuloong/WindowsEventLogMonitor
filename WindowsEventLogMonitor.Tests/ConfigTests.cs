using Newtonsoft.Json;
using WindowsEventLogMonitor.Services;

namespace WindowsEventLogMonitor.Tests;

/// <summary>
/// TDD tests for Config loading - verifies AutoStart section is properly initialized
/// </summary>
public class ConfigTests
{
    [Fact]
    public void Config_DefaultAutoStart_IsNotNull()
    {
        // Arrange & Act
        var config = new Config();
        
        // Assert
        Assert.NotNull(config.AutoStart);
    }
    
    [Fact]
    public void Config_DefaultAutoStart_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new Config();
        
        // Assert
        Assert.False(config.AutoStart.Enabled);
        Assert.Equal(AutoStartMode.Gui, config.AutoStart.Mode);
        Assert.True(config.AutoStart.MinimizeToTray);
    }
    
    [Fact]
    public void Config_DeserializeWithoutAutoStart_AutoStartIsNotNull()
    {
        // Arrange - JSON without AutoStart section (like the current config.json)
        var jsonWithoutAutoStart = @"{
            ""ApiUrl"": ""http://test:5000/api"",
            ""SqlServerMonitoring"": {
                ""Enabled"": true
            }
        }";
        
        // Act
        var config = JsonConvert.DeserializeObject<Config>(jsonWithoutAutoStart);
        
        // Assert - This is the critical test!
        // If AutoStart is null after deserialization, this is the bug!
        Assert.NotNull(config);
        Assert.NotNull(config.AutoStart); // THIS MAY FAIL - indicating the bug!
    }
    
    [Fact]
    public void Config_DeserializeWithAutoStart_PreservesValues()
    {
        // Arrange - JSON with AutoStart section
        var jsonWithAutoStart = @"{
            ""ApiUrl"": ""http://test:5000/api"",
            ""AutoStart"": {
                ""Enabled"": true,
                ""Mode"": 1,
                ""MinimizeToTray"": false
            }
        }";
        
        // Act
        var config = JsonConvert.DeserializeObject<Config>(jsonWithAutoStart);
        
        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.AutoStart);
        Assert.True(config.AutoStart.Enabled);
        Assert.Equal(AutoStartMode.Gui, config.AutoStart.Mode);
        Assert.False(config.AutoStart.MinimizeToTray);
    }
    
    [Fact]
    public void AutoStartConfig_Default_HasCorrectValues()
    {
        // Arrange & Act
        var autoStartConfig = new AutoStartConfig();
        
        // Assert
        Assert.False(autoStartConfig.Enabled);
        Assert.Equal(AutoStartMode.Gui, autoStartConfig.Mode);
        Assert.True(autoStartConfig.MinimizeToTray);
    }
}
