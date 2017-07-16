using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using WebRole.Models;

namespace WebRole
{
    /// <summary>
    /// Pattern for running special actions
    /// Special actions could be sending feedback to developer, calling IFTTT API, etc
    /// </summary>
    public interface ISpecialNoteAction
    {
        bool IsActionApplicable(string noteContents);
        Task PerformAction(string email, DateTime timestamp, string noteContents, string location);
    }

    public class ActionRunner
    {
        private static Lazy<List<ISpecialNoteAction>> _actionList = new Lazy<List<ISpecialNoteAction>>(() => FillActions());
        public static List<ISpecialNoteAction> FillActions()
        {
            var actionList = new List<ISpecialNoteAction>();
            actionList.Add(new FeedbackAction());
            return actionList;
        }
        public void RunActions(string email, DateTime timestamp, string noteContents, string location)
        {
            List<Task> actionList = new List<Task>();
            foreach(ISpecialNoteAction action in _actionList.Value)
            {
                if (action.IsActionApplicable(noteContents))
                {
                    Task actionTask = action.PerformAction(email, timestamp, noteContents, location);
                    actionTask.Start();
                    actionList.Add(actionTask);
                }
            }
            Task.WaitAll(actionList.ToArray());
        }
    }

    /// <summary>
    /// If note inclues #feedback, clone the feedback note to the developer
    /// </summary>
    public class FeedbackAction : ISpecialNoteAction
    {
        public bool IsActionApplicable(string noteContents)
        {
            return (noteContents.ToLowerInvariant().Contains("#feedback"));
        }

        public Task PerformAction(string email, DateTime timestamp, string noteContents, string location)
        {
            // Create a note for developer
            return new Task(() =>
            {
                if (!string.IsNullOrEmpty(email))
                {
                    string feedbackMessage = string.Concat(noteContents, "\n Email: ", email, "\n Location: ", location);
                    IndexerBase.CreateFeedbackNote(timestamp, feedbackMessage);
                }
            });
        }
    }
}