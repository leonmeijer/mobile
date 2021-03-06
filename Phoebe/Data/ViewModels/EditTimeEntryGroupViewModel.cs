using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class EditTimeEntryGroupViewModel : ViewModelBase, IDisposable
    {
        private TimeEntryModel model;
        private Timer durationTimer;
        private List<TimeEntryData> timeEntryList;

        EditTimeEntryGroupViewModel (TimeEntryModel model, List<TimeEntryData> timeEntryList)
        {
            this.model = model;
            this.timeEntryList = timeEntryList;
            this.durationTimer = new Timer();

            this.model.PropertyChanged += OnPropertyChange;
            this.durationTimer.Elapsed += DurationTimerCallback;

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Edit Grouped Time Entry";
            UpdateView ();
        }

        public static async Task<EditTimeEntryGroupViewModel> Init (List<string> timeEntryIds)
        {
            var timeEntryList = await GetTimeEntryDataList (timeEntryIds);
            var model = new TimeEntryModel (timeEntryList.Last ());
            await model.LoadAsync ();
            return new EditTimeEntryGroupViewModel (model, timeEntryList);
        }

        public void Dispose ()
        {
            durationTimer.Elapsed -= DurationTimerCallback;
            durationTimer.Dispose ();

            model.PropertyChanged -= OnPropertyChange;
            model = null;
        }

        #region viewModel State properties
        public bool IsRunning { get; set; }

        public string Duration { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime StopDate { get; set; }

        public string ProjectName { get; set; }

        public string ClientName { get; set; }

        public string Description { get; set; }

        public Guid WorkspaceId { get; set; }

        public int ProjectColor { get; set; }

        public ObservableRangeCollection<TimeEntryData> TimeEntryCollection  { get; set; }

        #endregion

        public async Task SetProjectAndTask (Guid projectId, Guid taskId)
        {
            Guid newWorkspaceId;

            // Set project to Model
            if (projectId == Guid.Empty) {
                model.Project = null;
                model.Task = null;
                newWorkspaceId = model.Workspace.Id;
            } else {
                var projectModel = new ProjectModel (projectId);
                await projectModel.LoadAsync ();

                model.Project = projectModel;
                model.Workspace = new WorkspaceModel (projectModel.Workspace);
                model.Task = taskId != Guid.Empty ? new TaskModel (taskId) : null;
                newWorkspaceId = projectModel.Workspace.Id;
            }

            // Edit in a straight way the time entries.
            foreach (var item in timeEntryList) {
                if (projectId == Guid.Empty) {
                    item.ProjectId = null;
                } else {
                    item.ProjectId = projectId;
                }

                if (taskId == Guid.Empty) {
                    item.TaskId = null;
                } else {
                    item.TaskId = projectId;
                }

                // Change workspace according to project selected
                item.WorkspaceId = newWorkspaceId;
                Model<TimeEntryData>.MarkDirty (item);
            }

            UpdateView ();
        }

        public void ChangeTimeEntryDuration (TimeSpan newDuration, TimeEntryData timeEntryData)
        {
            // TODO: for the moment, old style methods
            // using existing models.
            var entryModel = new TimeEntryModel (timeEntryData);
            entryModel.SetDuration (newDuration);
            timeEntryList [timeEntryList.IndexOf (p => p.Id == entryModel.Id)] = entryModel.Data;
            UpdateView ();

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Change Group Duration";
        }

        public async Task SaveModel ()
        {
            var dataStore = ServiceContainer.Resolve<IDataStore> ();
            var saveTasks = new List<Task> ();

            foreach (var item in timeEntryList) {
                item.Description = Description;
                Model<TimeEntryData>.MarkDirty (item);
                saveTasks.Add (dataStore.PutAsync (item));
            }

            await Task.WhenAll (saveTasks);
        }

        private void OnPropertyChange (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Data" ||
                    e.PropertyName == "StartTime" ||
                    e.PropertyName == "Duration" ||
                    e.PropertyName == "StopTime") {
                UpdateView ();
            }
        }

        private void UpdateView ()
        {
            // Ensure that this content runs in UI thread
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {

                StartDate = timeEntryList.FirstOrDefault ().StartTime.ToLocalTime ();
                StopDate = model.StopTime.HasValue ? model.StopTime.Value.ToLocalTime () : DateTime.UtcNow.ToLocalTime ();
                // TODO: check substring function for long times
                var listDuration = GetTimeEntryListDuration (timeEntryList);
                Duration = TimeSpan.FromSeconds (listDuration.TotalSeconds).ToString ().Substring (0, 8);
                Description = model.Description;
                ProjectName = model.Project != null ? model.Project.Name : string.Empty;
                ProjectColor = model.Project != null ? model.Project.Color : 0;
                WorkspaceId = model.Workspace.Id;

                if (model.Project != null) {
                    if (model.Project.Client != null) {
                        ClientName = model.Project.Client.Name;
                    }
                }

                if (model.State == TimeEntryState.Running && !IsRunning) {
                    IsRunning = true;
                    durationTimer.Start ();
                } else if (model.State != TimeEntryState.Running) {
                    IsRunning = false;
                    durationTimer.Stop ();
                }

                if (TimeEntryCollection == null) {
                    TimeEntryCollection = new ObservableRangeCollection<TimeEntryData> ();
                } else {
                    TimeEntryCollection.Clear ();
                }
                TimeEntryCollection.AddRange (timeEntryList);
            });
        }

        private void DurationTimerCallback (object sender, ElapsedEventArgs e)
        {
            var duration = model.GetDuration ();
            durationTimer.Interval = 1000 - duration.Milliseconds;

            // Update on UI Thread
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                Duration = TimeSpan.FromSeconds (duration.TotalSeconds).ToString ().Substring (0, 8);
            });
        }

        #region Time Entry list utils
        private static async Task<List<TimeEntryData>> GetTimeEntryDataList (List<string> ids)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var list = new List<TimeEntryData> (ids.Count);

            foreach (var stringGuid in ids) {
                var guid = new Guid (stringGuid);
                var rows = await store.Table<TimeEntryData> ()
                           .Where (r => r.Id == guid && r.DeletedAt == null)
                           .ToListAsync();
                list.Add (rows.FirstOrDefault ());
            }
            return list;
        }

        public TimeSpan GetTimeEntryListDuration (List<TimeEntryData> timeEntries)
        {
            TimeSpan duration = TimeSpan.Zero;
            foreach (var item in timeEntries) {
                duration += TimeEntryModel.GetDuration (item, Time.UtcNow);
            }
            return duration;
        }
        #endregion
    }
}

