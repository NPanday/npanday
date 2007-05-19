package NMaven.Plugin.Settings;

import org.apache.maven.dotnet.plugin.FieldAnnotation;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;

import java.io.File;

/**
 * @phase validate
 * @goal generate-settings
 */
public class SettingsGeneratorMojo
    extends org.apache.maven.dotnet.plugin.AbstractMojo
{
    /**
     * @parameter expression = "${project}"
     */
    private org.apache.maven.project.MavenProject project;

    /**
     * @parameter expression = "${settings.localRepository}"
     */
    private String localRepository;

    /**
     * @parameter expression = "${vendor}"
     */
    private String vendor;

    /**
     * @parameter expression = "${vendorVersion}"
     */
    private String vendorVersion;

    /**
     * @parameter expression = "${frameworkVersion}"
     */
    private String frameworkVersion;

    /**
     * @component
     */
    private org.apache.maven.dotnet.executable.NetExecutableFactory netExecutableFactory;

    /**
     * @component
     */
    private org.apache.maven.dotnet.plugin.PluginContext pluginContext;

    public String getMojoArtifactId()
    {
        return "NMaven.Plugin.Settings";
    }

    public String getMojoGroupId()
    {
        return "NMaven.Plugins";
    }

    public String getClassName()
    {
        return "NMaven.Plugin.Settings.SettingsGeneratorMojo";
    }

    public org.apache.maven.dotnet.plugin.PluginContext getNetPluginContext()
    {
        return pluginContext;
    }

    public org.apache.maven.dotnet.executable.NetExecutableFactory getNetExecutableFactory()
    {
        return netExecutableFactory;
    }

    public org.apache.maven.project.MavenProject getMavenProject()
    {
        return project;
    }

    public String getLocalRepository()
    {
        return localRepository;
    }

    public String getVendorVersion()
    {
        return vendorVersion;
    }

    public String getVendor()
    {
        return vendor;
    }

    public String getFrameworkVersion()
    {
        return frameworkVersion;
    }

    public boolean preExecute()
        throws MojoExecutionException, MojoFailureException
    {
        if ( !System.getProperty( "os.name" ).contains( "Windows" ) )
        {
            return false;
        }

        if ( System.getProperty( "bootstrap" ) != null )
        {
            return false;
        }

        String nmavenSettings =
            System.getProperty( "user.home" ) + File.separator + ".m2" + File.separator + "nmaven-settings.xml";

        return !new File( nmavenSettings ).exists();
    }
}
