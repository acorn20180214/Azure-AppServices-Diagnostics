﻿namespace Diagnostics.ModelsAndUtils.Models.ChangeAnalysis
{
    public class ChangeSetResponseModel
    {
        /// <summary>
        /// Represents key to a set of changes.
        /// </summary>
        public string ChangeSetId;

        /// <summary>
        /// Azure resource Id.
        /// </summary>
        public string ResourceId;

        /// <summary>
        /// Source of the change. It can be ARG, ARM or Site Extension.
        /// </summary>
        public string Source;

        /// <summary>
        /// Timestamp of the snapshot when the change was detected.
        /// </summary>
        public string TimeStamp;

        /// <summary>
        /// Interval of the change.
        /// </summary>
        public string TimeWindow;

        /// <summary>
        /// Change initiated by. It can be email address or Guid.
        /// </summary>
        public string InitiatedBy;
    }
}
