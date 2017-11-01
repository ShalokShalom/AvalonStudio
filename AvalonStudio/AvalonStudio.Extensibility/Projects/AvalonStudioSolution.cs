using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Projects;
using AvalonStudio.Platforms;
using AvalonStudio.Shell;
using AvalonStudio.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace AvalonStudio.Projects
{
    public class AvalonStudioSolution : ISolution
    {
        public const string Extension = "asln";

        public ObservableCollection<ISolutionItem> Items { get; }

        public AvalonStudioSolution()
        {
            ProjectReferences = new List<string>();
            Items = new ObservableCollection<ISolutionItem>();
            Parent = Solution = this;
        }

        public string StartupItem { get; set; }

        [JsonProperty("Projects")]
        public IList<string> ProjectReferences { get; set; }

        public T AddItem<T>(T item, ISolutionFolder parent = null) where T : ISolutionItem
        {
            if(item is IProject project)
            {
                var currentProject = Projects.FirstOrDefault(p => p.Name == project.Name);

                if (currentProject != null) return (T)currentProject;
                ProjectReferences.Add(CurrentDirectory.MakeRelativePath(project.Location));
                Items.InsertSorted(project);
                currentProject = project;

                return (T)currentProject;
            }

            return item;
        }

        public void RemoveItem(ISolutionItem item)
        {
            if(item is IProject project)
            {
                Items.Remove(project);
                ProjectReferences.Remove(CurrentDirectory.MakeRelativePath(project.Location).ToAvalonPath());
            }

        }

        public void VisitChildren(Action<ISolutionItem> visitor)
        {
            foreach (var child in Items)
            {
                if (child is ISolutionFolder folder)
                {
                    folder.VisitChildren(visitor);
                }

                visitor(child);
            }
        }

        public void Save()
        {
            StartupItem = StartupProject?.Name;

            for (var i = 0; i < ProjectReferences.Count; i++)
            {
                ProjectReferences[i] = ProjectReferences[i].ToAvalonPath();
            }

            SerializedObject.Serialize(Path.Combine(CurrentDirectory, Name + "." + Extension), this);
        }

        public ISourceFile FindFile(string file)
        {
            ISourceFile result = null;

            foreach (var project in Projects)
            {
                result = project.FindFile(file);

                if (result != null)
                {
                    break;
                }
            }

            return result;
        }

        [JsonIgnore]
        public string CurrentDirectory { get; set; }

        [JsonIgnore]
        public IEnumerable<IProject> Projects => Items.OfType<IProject>();

        [JsonIgnore]
        public IProject StartupProject { get; set; }

        [JsonIgnore]
        public string Name
        {
            get => Path.GetFileNameWithoutExtension(Location);
            set { }
        }

        [JsonIgnore]
        public bool CanRename => false;

        [JsonIgnore]
        public string Location { get; private set; }
        public ISolution Solution { get; set; }
        public ISolutionFolder Parent { get; set; }

        public Guid Id { get; set; }        

        private static IProject LoadProject(ISolution solution, string reference)
        {
            var shell = IoC.Get<IShell>();
            IProject result = null;

            var extension = Path.GetExtension(reference).Remove(0, 1);

            var projectType = shell.ProjectTypes.FirstOrDefault(p => p.Extensions.Contains(extension));
            var projectFilePath = Path.Combine(solution.CurrentDirectory, reference).ToPlatformPath();

            if (projectType != null && System.IO.File.Exists(projectFilePath))
            {
                result = projectType.Load(solution, projectFilePath);
            }
            else
            {
                Console.WriteLine("Failed to load " + projectFilePath);
            }

            return result;
        }

        public static VisualStudioSolution ConvertToSln (ISolution solution)
        {
            var result = VisualStudioSolution.Create(solution.CurrentDirectory, solution.Name, true, AvalonStudioSolution.Extension);

            foreach(var item in solution.Items)
            {
                if(item is IProject project)
                {
                    result.AddItem(project);
                }
            }

            result.StartupProject = solution.StartupProject;

            result.Save();

            return result;
        }

        public static ISolution Load(string fileName)
        {
            try
            {
                return VisualStudioSolution.Load(fileName);
            }
            catch (Exception e)
            { 
                var solution = SerializedObject.Deserialize<AvalonStudioSolution>(fileName);

                solution.Location = fileName.NormalizePath().ToPlatformPath();
                solution.CurrentDirectory = (Path.GetDirectoryName(fileName) + Platform.DirectorySeperator).ToPlatformPath();

                foreach (var projectReference in solution.ProjectReferences)
                {
                    var proj = LoadProject(solution, projectReference);

                    // todo null returned here we need a placeholder.
                    if (proj != null)
                    {
                        proj.Solution = solution;
                        solution.Items.InsertSorted(proj);
                    }
                }

                foreach (var project in solution.Projects)
                {
                    project.ResolveReferences();
                }

                solution.StartupProject = solution.Projects.SingleOrDefault(p => p.Name == solution.StartupItem);

                var console = IoC.Get<IConsole>();

                console.WriteLine("Migrating ASLN to SLN format. Opening this file again will overwrite the newly created SLN file.");
                console.WriteLine("Please delte ASLN file when you are happy with the migration.");

                return ConvertToSln(solution);
            }
        }

        public IProject FindProject(string name)
        {
            throw new NotImplementedException();
        }

        public int CompareTo(ISolutionItem other)
        {
            return this.DefaultCompareTo(other);
        }

        public void UpdateItem(ISolutionItem item)
        {
            throw new NotImplementedException();
        }
    }
}