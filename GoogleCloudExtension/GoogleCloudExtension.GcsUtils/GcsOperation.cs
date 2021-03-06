﻿// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using GoogleCloudExtension.DataSources;
using GoogleCloudExtension.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GoogleCloudExtension.GcsUtils
{
    /// <summary>
    /// Base class for all GCS operations, serves as a model that can be used in UI to represent
    /// the progress and state of the operation.
    /// </summary>
    public abstract class GcsOperation : Model, IGcsFileOperationCallback
    {
        private readonly SynchronizationContext _context;
        private double _progress = 0;
        private bool _isError;
        private bool _isCancelled;
        private bool _isPending = true;

        /// <summary>
        /// The current progress of the operation, between 0.0 (started) to 1.0 (completed).
        /// </summary>
        public double Progress
        {
            get { return _progress; }
            private set { SetValueAndRaise(ref _progress, value); }
        }

        /// <summary>
        /// Whether this operation is in error.
        /// </summary>
        public bool IsError
        {
            get { return _isError; }
            private set { SetValueAndRaise(ref _isError, value); }
        }

        /// <summary>
        /// Whether this operation is cancelled.
        /// </summary>
        public bool IsCancelled
        {
            get { return _isCancelled; }
            private set { SetValueAndRaise(ref _isCancelled, value); }
        }

        /// <summary>
        /// Whether this operation is still waiting in the queue or processing has started.
        /// </summary>
        public bool IsPending
        {
            get { return _isPending; }
            private set
            {
                var changed = value != _isPending;
                SetValueAndRaise(ref _isPending, value);
                if (changed && !_isPending)
                {
                    Started?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        ///  The main GCS item for the operation.
        /// </summary>
        public abstract GcsItemRef GcsItem { get; }

        /// <summary>
        /// Event raised when the operation completes.
        /// </summary>
        public event EventHandler Completed;

        /// <summary>
        /// Event raised when the operation is started.
        /// </summary>
        public event EventHandler Started;

        protected GcsOperation()
        {
            _context = SynchronizationContext.Current;
        }

        #region IGcsFileOperationCallback implementation.

        void IGcsFileOperationCallback.Cancelled()
        {
            _context.Send((x) =>
            {
                IsCancelled = true;
                IsPending = false;
                Progress = 0.0;
                Completed?.Invoke(this, EventArgs.Empty);
            }, null);
        }

        void IGcsFileOperationCallback.Completed()
        {
            _context.Send((x) =>
            {
                IsPending = false;
                Progress = 1.0;
                Completed?.Invoke(this, EventArgs.Empty);
            }, null);
        }

        void IGcsFileOperationCallback.Error(DataSourceException ex)
        {
            _context.Send((x) =>
            {
                IsError = true;
                IsPending = false;
                Progress = 0.0;
                Completed?.Invoke(this, EventArgs.Empty);
            }, null);
        }

        void IGcsFileOperationCallback.Progress(double value)
        {
            _context.Send((x) =>
            {
                if (value > 0.0)
                {
                    IsPending = false;
                }
                Progress = value;
            }, null);
        }

        #endregion

        /// <summary>
        /// Returns a task that will completed once the operation is completed. The error state will not be
        /// sent to this task.
        /// </summary>
        internal Task AwaitOperationAsync()
        {
            var taskCompletion = new TaskCompletionSource<int>();
            Completed += (o, e) => taskCompletion.SetResult(0);
            return taskCompletion.Task;
        }
    }
}
