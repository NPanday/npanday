#region Apache License, Version 2.0
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion

#region Using
using Extensibility;
using EnvDTE;
using EnvDTE80;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Drawing;
using System.Threading;
using System.Web.Services.Protocols;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.VisualStudio.CommandBars;

using VSLangProj;

using NMaven.Artifact;
using NMaven.Logging;
using NMaven.Service;
using NMaven.VisualStudio.Logging;

using NMaven.IDE;
using NMaven.IDE.Impl;

using Castle.Windsor;
using Castle.Windsor.Configuration.Interpreters;

using NMaven.Model.Setting;
using NMaven.Model.Pom;
 
#endregion
  
  namespace NMaven.VisualStudio.Addin
  {
    #region Connect
     /// <summary>The object for implementing an Add-in.</summary>
     /// <seealso class='IDTExtensibility2' />
     public class Connect : IDTExtensibility2, IDTCommandTarget
     {
        #region Connect()
        /// <summary>
        /// Implements the constructor for the Add-in object.
        /// Place your initialization code within this method.
        /// </summary>
        public Connect()
        {
  
        }
        #endregion
  
        #region OnConnection(object,ext_ConnectMode,object,Array)
        /// <summary>
        /// Implements the OnConnection method of the IDTExtensibility2 interface.
        /// Receives notification that the Add-in is being loaded.
        /// </summary>
        /// <param term='application'>Root object of the host application.</param>
        /// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
        /// <param term='addInInst'>Object representing this Add-in.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _applicationObject = (DTE2)application;
            _addInInstance = (AddIn)addInInst;
            Command command = null;
            if (connectMode == ext_ConnectMode.ext_cm_UISetup)
            {
                object[] contextGUIDS = new object[] { };
                Commands2 commands = (Commands2)_applicationObject.Commands;
                string toolsMenuName;

                try
                {
                    //If you would like to move the command to a different menu, change the word "Tools" to the
                    //  English version of the menu. This code will take the culture, append on the name of the menu
                    //  then add the command to that menu. You can find a list of all the top-level menus in the file
                    //  CommandBar.resx.
                    ResourceManager resourceManager = new ResourceManager("IDEAddin.CommandBar", Assembly.GetExecutingAssembly());
                    CultureInfo cultureInfo = new System.Globalization.CultureInfo(_applicationObject.LocaleID);
                    string resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, "Tools");
                    toolsMenuName = resourceManager.GetString(resourceName);
                }
                catch
                {
                    //We tried to find a localized version of the word Tools, but one was not found.
                    //  Default to the en-US word, which may work for the current culture.
                    toolsMenuName = "Tools";
                }

                //Place the command on the tools menu.
                //Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
                Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

                //Find the Tools command bar on the MenuBar command bar:
                CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
                CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

                //This try/catch block can be duplicated if you wish to add multiple commands to be handled by your Add-in,
                //  just make sure you also update the QueryStatus/Exec method to include the new command names.
                try
                {
                    //Add a command to the Commands collection:
                    command = commands.AddNamedCommand2(_addInInstance, "NMavenAddin",
                        "NMaven Build System", "Executes the command for NMavenAddin", true, 480, ref contextGUIDS,
                        (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                        (int)vsCommandStyle.vsCommandStylePictAndText,
                        vsCommandControlType.vsCommandControlTypeButton);

                    //Add a control for the command to the tools menu:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //If we are here, then the exception is probably because a command with that name
                    //  already exists. If so there is no need to recreate the command and we can
                    //  safely ignore the exception.
                }
            }
            else if (connectMode == ext_ConnectMode.ext_cm_AfterStartup)
            {
                Window win = _applicationObject.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                OutputWindow outputWindow = (OutputWindow)win.Object;
                outputWindowPane = outputWindow.OutputWindowPanes.Add("NMaven Build System");

                OutputWindowPaneHandler handler = new OutputWindowPaneHandler();
                handler.SetOutputWindowPaneHandler(outputWindowPane);

                logger = NMaven.Logging.Logger.GetLogger("UC");
                logger.AddHandler(handler);

                 string localRepository = SettingsUtil.GetLocalRepositoryPath();
                 string contents =
 @"<configuration>
   <components>
     <component id=""smtp.sender""
                type=""NMaven.Artifact.ArtifactContext, NMaven.Artifact""/>
   </components>
 </configuration>";
  
                container = new WindsorContainer(new XmlInterpreter(new Castle.Core.Resource.StaticContentResource(contents)));
                ArtifactContext artifactContext = (ArtifactContext)container[typeof(ArtifactContext)];
                object[] attributes = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), true);
                String version = ((AssemblyInformationalVersionAttribute) attributes[0]).InformationalVersion;

                NMaven.Artifact.Artifact artifactWar = artifactContext.CreateArtifact("org.apache.maven.dotnet",
                    "dotnet-service-embedder", version, "war");
                FileInfo warFileInfo = new FileInfo(localRepository + "/" + new JavaRepositoryLayout().pathOf(artifactWar) + "war");
                logger.Log(Level.INFO, "Executing external command plugin: "
                    + @"mvn org.apache.maven.dotnet.plugins:maven-embedder-plugin:start -Dport=8080 -DwarFile="""
                    + warFileInfo.FullName + @"""");

                ProcessStartInfo processStartInfo =
                    new ProcessStartInfo("mvn", @"org.apache.maven.dotnet.plugins:maven-embedder-plugin:start -Dport=8080 -DwarFile="""
                     + warFileInfo.FullName + @"""");
                processStartInfo.UseShellExecute = true;
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(processStartInfo);

                EnvDTE80.Windows2 windows2 = (EnvDTE80.Windows2)_applicationObject.Windows;
                _applicationObject = (DTE2)application;

                ideContext = new IdeContextImpl();

                IIdeConfiguration configuration = Factory.CreateIdeConfiguration();
                configuration.Logger = logger;
                configuration.SocketLoggerPort = 9099;
                ideContext.Init(configuration);

                DTE2 dte2 = _applicationObject;
            
                addReferenceControls = new List<CommandBarButton>();
                buildControls = new List<CommandBarControl>();
                foreach (CommandBar commandBar in (CommandBars)dte2.CommandBars)
                {
                    foreach (CommandBarControl control in commandBar.Controls)
                    {
                        if (control.Caption.Equals("Add &Reference..."))
                        {
                           // outputWindowPane.OutputString("Adding control reference: " + commandBar.Name + Environment.NewLine);

                            CommandBarButton ctl = (CommandBarButton)
                                commandBar.Controls.Add(MsoControlType.msoControlButton,
                                System.Type.Missing, System.Type.Missing, control.Index, true);
                            ctl.Click += new _CommandBarButtonEvents_ClickEventHandler(cbShowAddArtifactsForm_Click);
                            ctl.Caption = "Add Maven Artifact...";
                            ctl.Visible = true;
                            addReferenceControls.Add(ctl);

                            CommandBarButton ctl1 = (CommandBarButton)
                                commandBar.Controls.Add(MsoControlType.msoControlButton, 
                                System.Type.Missing, System.Type.Missing, control.Index, true);
                            ctl1.Click += 
                                new _CommandBarButtonEvents_ClickEventHandler(cbShowConfigureRepositoryForm_Click);
                            ctl1.Caption = "Configure Maven Repository...";
                            ctl1.Visible = true;
                            addReferenceControls.Add(ctl1);
                        }
                        else if (control.Caption.Equals("Clea&n"))
                        {
                            CommandBarPopup ctl = (CommandBarPopup)
                                commandBar.Controls.Add(MsoControlType.msoControlPopup,
                                System.Type.Missing, System.Type.Missing, control.Index + 1, true);
                            ctl.Caption = "Maven Phase";
                            ctl.Visible = true;
                            buildControls.Add(ctl);

                            CommandBarButton cleanButton = (CommandBarButton)ctl.Controls.Add(MsoControlType.msoControlButton,
                                System.Type.Missing, System.Type.Missing, 1, true);
                            cleanButton.Caption = "Clean";
                            cleanButton.Visible = true;
                            cleanButton.Click += new _CommandBarButtonEvents_ClickEventHandler(cbClean_Click);


                            CommandBarButton testButton = (CommandBarButton)ctl.Controls.Add(MsoControlType.msoControlButton,
                                System.Type.Missing, System.Type.Missing, 1, true);
                            testButton.Caption = "Test";
                            testButton.Visible = true;
                            testButton.Click += new _CommandBarButtonEvents_ClickEventHandler(cbTest_Click);

                            CommandBarButton installButton = (CommandBarButton)ctl.Controls.Add(MsoControlType.msoControlButton,
                                System.Type.Missing, System.Type.Missing, 1, true);
                            installButton.Caption = "Install";
                            installButton.Visible = true;
                            installButton.Click += new _CommandBarButtonEvents_ClickEventHandler(cbInstall_Click);

                            CommandBarButton buildButton = (CommandBarButton)ctl.Controls.Add(MsoControlType.msoControlButton,
                                System.Type.Missing, System.Type.Missing, 1, true);
                            buildButton.Caption = "Build";
                            buildButton.Visible = true;
                            buildButton.FaceId = 645;
                            buildButton.Click += new _CommandBarButtonEvents_ClickEventHandler(cbBuild_Click);

                            buildControls.Add(buildButton);
                            buildControls.Add(installButton);
                            buildControls.Add(cleanButton);
                            buildControls.Add(testButton);
                        }
                    }
                }
                nunitControls = new List<CommandBarButton>();
                Window solutionExplorerWindow = dte2.Windows.Item(Constants.vsWindowKindSolutionExplorer);
                _selectionEvents = dte2.Events.SelectionEvents;
                _selectionEvents.OnChange += new _dispSelectionEvents_OnChangeEventHandler(this.OnChange);

                //References
                referenceEvents = new List<ReferencesEvents>();
                foreach (Project project in _applicationObject.Solution.Projects)
                {
                    VSProject vsProject = (VSProject)project.Object;
                    referenceEvents.Add(vsProject.Events.ReferencesEvents);
                    vsProject.Events.ReferencesEvents.ReferenceRemoved 
                        += new _dispReferencesEvents_ReferenceRemovedEventHandler(ReferencesEvents_ReferenceRemoved);
                }
            }
        }
        #endregion

         void ReferencesEvents_ReferenceRemoved(Reference pReference)
         {
             MessageBox.Show("Removing Reference");
             Project project = pReference.ContainingProject;

             String pomFileName =
                 (new FileInfo(project.FileName).Directory).FullName + @"\pom.xml";

             if (!new FileInfo(pomFileName).Exists)
             {
                 MessageBox.Show("Could not delete reference. Missing pom file: File = " + pomFileName);
                 return;
             }

             XmlReader reader = XmlReader.Create(pomFileName);
             XmlSerializer serializer = new XmlSerializer(typeof(NMaven.Model.Pom.Model));
             if (!serializer.CanDeserialize(reader))
             {
                 MessageBox.Show("Could not delete reference. Corrupted pom file: File = " + pomFileName);
                 return;
             }

             NMaven.Model.Pom.Model model = (NMaven.Model.Pom.Model)serializer.Deserialize(reader);
             List<Dependency> newDependencies = new List<Dependency>();
             if(model.dependencies != null)
             {
                 foreach (Dependency dependency in model.dependencies)
                 {
                     if (!pReference.Name.Equals(dependency.artifactId))
                     {
                         newDependencies.Add(dependency);   
                     }
                 }
                 model.dependencies = newDependencies.ToArray();
                 reader.Close();

                 TextWriter writer = new StreamWriter(pomFileName);
                 serializer.Serialize(writer, model);
                 writer.Close();
             }
         }

        #region OnChange()
        public void OnChange()
        {
            foreach (SelectedItem item in _applicationObject.SelectedItems)
            {
                if (item.Name.EndsWith("Test.cs"))
                {
                    if (nunitControls.Count == 0)
                    {
                        DTE2 dte2 = _applicationObject;
                        foreach (CommandBar commandBar in (CommandBars)dte2.CommandBars)
                        {
                            foreach (CommandBarControl control in commandBar.Controls)
                            {
                                if (control.Caption.Equals("View &Code"))
                                {
                                    CommandBarButton nunitControl = (CommandBarButton)
                                        commandBar.Controls.Add(MsoControlType.msoControlButton,
                                        System.Type.Missing, System.Type.Missing, control.Index, true);
                                    nunitControl.Click += new _CommandBarButtonEvents_ClickEventHandler(cbRunUnitTest_Click);
                                    nunitControl.Caption = "Run Unit Test";
                                    nunitControl.Visible = true;
                                    CommandBarButton nunitCompileAndRunControl = (CommandBarButton)
                                        commandBar.Controls.Add(MsoControlType.msoControlButton,
                                        System.Type.Missing, System.Type.Missing, control.Index, true);
                                    nunitCompileAndRunControl.Click
                                        += new _CommandBarButtonEvents_ClickEventHandler(cbCompileAndRunUnitTest_Click);
                                    nunitCompileAndRunControl.Caption = "Compile and Run Unit Test";
                                    nunitCompileAndRunControl.Visible = true;
                                    nunitControls.Add(nunitControl);
                                    nunitControls.Add(nunitCompileAndRunControl);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (CommandBarButton button in nunitControls)
                        {
                            button.Visible = true;
                        }
                    }
                }
                else
                {
                    foreach (CommandBarButton button in nunitControls)
                    {
                        button.Visible = false;
                    }
                }
            }
        }
        #endregion

        #region ClearOutputWindowPane(object,EventArgs)
        private void ClearOutputWindowPane(object sender, EventArgs args)
        {
            outputWindowPane.Clear();
        }
        #endregion

        #region ActivateOutputWindowPane(object,EventArgs)
        private void ActivateOutputWindowPane(object sender, EventArgs args)
        {
            outputWindowPane.Activate();
        }
        #endregion

        #region OnDisconnection(ext_DisconnectMode,Array)
        /// <summary>
        /// Implements the OnDisconnection method of the IDTExtensibility2 interface. 
        /// Receives notification that the Add-in is being unloaded.
        /// </summary>
        /// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {

        }
        #endregion

        #region OnAddInsUpdate(Array)
        /// <summary>
        /// Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. 
        /// Receives notification when the collection of Add-ins has changed.
        /// </summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnAddInsUpdate(ref Array custom)
        {
        }
        #endregion

        #region OnStartupComplete(Array)
        /// <summary>
        /// Implements the OnStartupComplete method of the IDTExtensibility2 interface. 
        /// Receives notification that the host application has completed loading.
        /// </summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom)
        {

        }
        #endregion

        #region executeBuildCommand(FileInfo,string)
        private void executeBuildCommand(FileInfo pomFile, String goal)
        {
            MavenExecutionRequest request = new MavenExecutionRequest();
            request.goal = goal;
            request.pomFile = @pomFile.FullName;
            request.loggerPort = 9099;
            request.loggerPortSpecified = true;

            try
            {
                ideContext.Build(request);
            }
            catch (SoapException e)
            {
                string message = string.Format(
                    "NMaven: Soap Error in build: {0}, {1}, {2}",
                    e.Code,
                    e.SubCode,
                    e.StackTrace);

                ideContext.GetLogger().Log(Level.INFO, message);
            }
            catch (Exception e)
            {
                ideContext.GetLogger().Log(Level.INFO, "NMaven: Error in build: " + e.Message + ", Pom File = " + pomFile.FullName +
                    "," + e.StackTrace);
            }
        } 
        #endregion

        #region getPomFile()
        private FileInfo getPomFile()
        {
            FileInfo projectFileInfo = null;
            //TODO: Fix to handle multiple projects: NMAVEN-80
            foreach (Project project in (Array)_applicationObject.ActiveSolutionProjects)
            {
                ideContext.GetLogger().Log(Level.INFO, "project.FileName = " + project.FileName);
                projectFileInfo = new FileInfo(project.FileName);
                break;
            }
            FileInfo pomFile = new FileInfo(projectFileInfo.DirectoryName + @"\pom.xml");
            if (!pomFile.Exists)
            {
                pomFile = new FileInfo(projectFileInfo.Directory.Parent.Parent.Parent.FullName + @"\pom.xml");
            }
            return pomFile;
        } 
        #endregion

        #region cbRunUnitTest_Click(CommandBarButton,bool)
        private void cbRunUnitTest_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "org.apache.maven.dotnet.plugins:maven-test-plugin:test");
        } 
        #endregion

        #region cbCompileAndRunUnitTest_Click(CommandBarButton,bool)
        private void cbCompileAndRunUnitTest_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "test");
        } 
        #endregion

        #region cbInstall_Click(CommandBarButton,bool)
        private void cbInstall_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "install");
        } 
        #endregion

        #region cbClean_Click(CommandBarButton,bool)
        private void cbClean_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "clean");
        } 
        #endregion

        #region cbBuild_Click(CommandBarButton,bool)
        private void cbBuild_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "compile");
        } 
        #endregion

        #region cbTest_Click(CommandBarButton,bool)
        private void cbTest_Click(CommandBarButton btn, ref bool Cancel)
        {
            executeBuildCommand(getPomFile(), "test");
        } 
        #endregion

         private void cbShowConfigureRepositoryForm_Click(CommandBarButton btn, ref bool Cancel)
         {
            new ConfigureMavenRepositoryForm().Show();
         }

        #region cbShowAddArtifactsForm_Click(CommandBarButton,bool)
        private void cbShowAddArtifactsForm_Click(CommandBarButton btn, ref bool Cancel)
        {
            //First selected project
            foreach (Project project in (Array)_applicationObject.ActiveSolutionProjects)
            {
                AddArtifactsForm form = new AddArtifactsForm(project, container);
                form.Show();
                break;
            }
        } 
        #endregion

        #region OnBeginShutdown(Array)
        /// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom)
        {
            WebClient webClient = new WebClient();
            webClient.DownloadData("http://localhost:8080?shutdown=true");
        } 
        #endregion

        #region QueryStatus(string,vsCommandStatusTextWanted,vsCommandStatus,commandText)
        /// <summary>
        /// Implements the QueryStatus method of the IDTCommandTarget interface. 
        /// This is called when the command's availability is updated.
        /// </summary>
        /// <param term='commandName'>The name of the command to determine state for.</param>
        /// <param term='neededText'>Text that is needed for the command.</param>
        /// <param term='status'>The state of the command in the user interface.</param>
        /// <param term='commandText'>Text requested by the neededText parameter.</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
            {
                if (commandName == "IDEAddin.Connect.IDEAddin")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
        } 
        #endregion

        #region Exec(string,vsCommandExecOption,object,object,bool)
        /// <summary>
        /// Implements the Exec method of the IDTCommandTarget interface. 
        /// This is called when the command is invoked.
        /// </summary>
        /// <param term='commandName'>The name of the command to execute.</param>
        /// <param term='executeOption'>Describes how the command should be run.</param>
        /// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
        /// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
        /// <param term='handled'>Informs the caller if the command was handled or not.</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            outputWindowPane.OutputString(commandName);
            handled = false;
            if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
            {
                handled = true;
            }
        } 
        #endregion

        #region Fields
        private DTE2 _applicationObject;
        private AddIn _addInInstance;
        private OutputWindowPane outputWindowPane;
        private NMaven.Logging.Logger logger;
        private List<CommandBarButton> addReferenceControls;
        private List<CommandBarButton> nunitControls;
        private List<CommandBarControl> buildControls;
        private EnvDTE.SelectionEvents _selectionEvents;
        private IIdeContext ideContext;
        private IWindsorContainer container;
         private List<ReferencesEvents> referenceEvents;
        //private DirectoryInfo baseDirectoryInfo; 
        #endregion
    }
    #endregion
}