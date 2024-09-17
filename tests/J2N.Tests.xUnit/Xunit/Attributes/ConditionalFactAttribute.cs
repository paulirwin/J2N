﻿// From https://github.com/dotnet/arcade

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;
using Xunit.Sdk;

namespace Xunit
{
    [XunitTestCaseDiscoverer("Xunit.ConditionalFactDiscoverer", "J2N.Tests.xUnit")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConditionalFactAttribute : FactAttribute
    {
        public Type CalleeType { get; private set; }
        public string[] ConditionMemberNames { get; private set; }

        public ConditionalFactAttribute(Type calleeType, params string[] conditionMemberNames)
        {
            CalleeType = calleeType;
            ConditionMemberNames = conditionMemberNames;
        }

        public ConditionalFactAttribute(params string[] conditionMemberNames)
        {
            ConditionMemberNames = conditionMemberNames;
        }
    }
}