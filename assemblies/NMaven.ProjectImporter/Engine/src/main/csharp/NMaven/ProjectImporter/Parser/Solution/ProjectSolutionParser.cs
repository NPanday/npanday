using System;
using System.Collections.Generic;
using System.Text;
using NMaven.ProjectImporter.SlnParser;
using NMaven.ProjectImporter.SlnParser.Model;
using System.IO;
using NMaven.ProjectImporter.Parser.VisualStudioProjectTypes;
using System.Text.RegularExpressions;

namespace NMaven.ProjectImporter.Parser.Solution
{
    public class ProjectSolutionParser : AbstractSolutionParserAlgorithm
    {
        public override List<Dictionary<string, object>> Parse(FileInfo solutionFile)
        {

            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            NMaven.ProjectImporter.SlnParser.Model.Solution solution = SolutionFactory.GetSolution(solutionFile);


            foreach (Project project in solution.Projects)
            {
                Dictionary<string, object> dictionary = new Dictionary<string, object>();


                dictionary.Add("ProjectTypeGuid", project.ProjectTypeGUID);
                dictionary.Add("ProjectType", VisualStudioProjectType.GetVisualStudioProjectType(project.ProjectTypeGUID));

                dictionary.Add("ProjectName", project.ProjectName);
                dictionary.Add("ProjectPath", project.ProjectPath);
                dictionary.Add("ProjectGUID", project.ProjectGUID);

                string fullpath = Path.Combine(solutionFile.DirectoryName, project.ProjectPath);
                dictionary.Add("ProjectFullPath", fullpath);






                // this is for web projects
                if ((VisualStudioProjectTypeEnum)dictionary["ProjectType"] == VisualStudioProjectTypeEnum.Web_Site)
                {

                    string[] assemblies = GetWebConfigAssemblies(Path.Combine(fullpath, "web.config"));
                    dictionary.Add("WebConfigAssemblies", assemblies);


                    //@001 SERNACIO START retrieving webreference
                    Digest.Model.WebReferenceUrl[] webReferences = getWebReferenceUrls(fullpath);
                    dictionary.Add("WebReferencesUrl", webReferences);
                    //@001 SERNACIO END retrieving webreference

                    string[] binAssemblies = GetBinAssemblies(Path.Combine(fullpath, @"bin"));
                    dictionary.Add("BinAssemblies", binAssemblies);
                    //ParseInnerData(dictionary, match.Groups["projectInnerData"].ToString());
                    ParseProjectReferences(dictionary, project, solution);
                }
                // this is for normal projects
                else if (
                    (VisualStudioProjectTypeEnum)dictionary["ProjectType"] == VisualStudioProjectTypeEnum.Windows__CSharp
                    || (VisualStudioProjectTypeEnum)dictionary["ProjectType"] == VisualStudioProjectTypeEnum.Windows__VbDotNet
                    )
                {
                    Microsoft.Build.BuildEngine.Project prj = new Microsoft.Build.BuildEngine.Project(BUILD_ENGINE);
                    prj.Load(fullpath);
                    //ParseInnerData(dictionary, match.Groups["projectInnerData"].ToString());
                    ParseProjectReferences(dictionary, project, solution);
                    dictionary.Add("Project", prj);
                }

                list.Add(dictionary);


            }

            return list;
        }



        protected void ParseProjectReferences(Dictionary<string, object> dictionary, Project project, NMaven.ProjectImporter.SlnParser.Model.Solution solution)
        {
            if (project.ProjectSections != null)
            {
                List<Microsoft.Build.BuildEngine.Project> projectReferenceList = new List<Microsoft.Build.BuildEngine.Project>();
                foreach (ProjectSection ps in project.ProjectSections)
                {
                    if ("WebsiteProperties".Equals(ps.Name))
                    {
                        // ProjectReferences = "{11F2FCC8-5941-418A-A0E7-42D250BA9D21}|SampleInterProject111.dll;{9F37BA7B-06F9-4B05-925D-B5BC16322E8B}|BongClassLib.dll;"

                        try
                        {
                            Regex regex = new Regex(PROJECT_REFERENCE_REGEX, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                            MatchCollection matches = regex.Matches(ps.Map["ProjectReferences"]);


                            foreach (Match match in matches)
                            {
                                string projectReferenceGUID = match.Groups["ProjectReferenceGUID"].ToString();
                                string projectReferenceDll = match.Groups["ProjectReferenceDll"].ToString();
                                
                                Microsoft.Build.BuildEngine.Project prj = GetMSBuildProject(solution, projectReferenceGUID);
                                if (prj != null)
                                {
                                    projectReferenceList.Add(prj);
                                }
                            }
                        }
                        catch { }




                    }
                    else if("ProjectDependencies".Equals(ps.Name))
                    {
                        // TODO: implemtation here

                        //{0D80BE11-F1CE-409E-B9AC-039D3801209F} = {0D80BE11-F1CE-409E-B9AC-039D3801209F}

                        foreach (string key in ps.Map.Keys)
                        {
                            Microsoft.Build.BuildEngine.Project prj = GetMSBuildProject(solution, key.Replace("{","").Replace("}", ""));
                            if (prj != null)
                            {
                                projectReferenceList.Add(prj);
                            }
                        }

                    }
                }

                dictionary.Add("InterProjectReferences", projectReferenceList.ToArray());
            }


            
        }

        Microsoft.Build.BuildEngine.Project GetMSBuildProject(NMaven.ProjectImporter.SlnParser.Model.Solution solution, string projectGuid)
        {

            foreach (Project p in solution.Projects)
            {
                if (p.ProjectGUID.Equals("{" + projectGuid + "}", StringComparison.OrdinalIgnoreCase))
                {
                    string projectReferenceName = p.ProjectName;
                    string projectReferencePath = p.ProjectPath;
                    string projectReferenceFullPath = null;

                    if (Path.IsPathRooted(projectReferencePath))
                    {
                        projectReferenceFullPath = Path.GetFullPath(projectReferencePath);
                    }
                    else
                    {
                        projectReferenceFullPath = Path.Combine(solution.File.Directory.FullName, projectReferencePath);
                    }


                    Microsoft.Build.BuildEngine.Project prj = new Microsoft.Build.BuildEngine.Project(BUILD_ENGINE);
                    prj.Load(projectReferenceFullPath);

                    return prj;

                }
            }

            return null;
        }



        Digest.Model.WebReferenceUrl[] getWebReferenceUrls(string projectPath)
        {
            List<Digest.Model.WebReferenceUrl> returnList = new List<Digest.Model.WebReferenceUrl>();
            string webPath = Path.GetFullPath(Path.Combine(projectPath, "App_WebReferences"));
            if (Directory.Exists(webPath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(webPath);
                foreach (DirectoryInfo folders in dirInfo.GetDirectories())
                {
                    if (folders.Equals(".svn")) continue;
                    returnList.AddRange(getWebReferenceUrls(folders, "App_WebReferences"));
                }
            }
            return returnList.ToArray();
        }

        Digest.Model.WebReferenceUrl[] getWebReferenceUrls(DirectoryInfo folder, string currentPath)
        {
            string relPath = Path.Combine(currentPath, folder.Name);
            string url = string.Empty;
            List<Digest.Model.WebReferenceUrl> webReferenceUrls = new List<Digest.Model.WebReferenceUrl>();

            FileInfo[] fileInfo = folder.GetFiles("*.discomap");
            if (fileInfo != null && fileInfo.Length > 0)
            {
                System.Xml.XPath.XPathDocument xDoc = new System.Xml.XPath.XPathDocument(fileInfo[0].FullName);
                System.Xml.XPath.XPathNavigator xNav = xDoc.CreateNavigator();
                string xpathExpression = @"DiscoveryClientResultsFile/Results/DiscoveryClientResult[@referenceType='System.Web.Services.Discovery.ContractReference']/@url";
                System.Xml.XPath.XPathNodeIterator xIter = xNav.Select(xpathExpression);
                if (xIter.MoveNext())
                {
                    url = xIter.Current.TypedValue.ToString();
                }
            }
            if (!string.IsNullOrEmpty(url))
            {
                Digest.Model.WebReferenceUrl newWebReferenceUrl = new Digest.Model.WebReferenceUrl();
                newWebReferenceUrl.RelPath = relPath;
                newWebReferenceUrl.UpdateFromURL = url;
                webReferenceUrls.Add(newWebReferenceUrl);
            }
            foreach (DirectoryInfo dirInfo in folder.GetDirectories())
            {
                webReferenceUrls.AddRange(getWebReferenceUrls(dirInfo, relPath));
            }
            return webReferenceUrls.ToArray();
        }


    }
}