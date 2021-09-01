////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;

namespace Holofunk.StateMachines
{
    /// <summary>A model can be updated.</summary>
    /// <remarks>Lets the current model define the current update function to be applied to it at the current moment..</remarks>
    public abstract class Model
    {
        /// <summary>
        /// Update the model at the current moment.
        /// </summary>
        public abstract void Update(Moment now);
    }
}
