// --------------------------------------------------------------------------------------------------
//  <copyright file="AssertFailedException.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2015 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------

namespace MsTestRunner
{
    using System;

    public sealed class AssertFailedException : Exception
    {
        #region Constructors and Destructors

        public AssertFailedException(string message)
            : base(message)
        {
        }

        #endregion
    }
}