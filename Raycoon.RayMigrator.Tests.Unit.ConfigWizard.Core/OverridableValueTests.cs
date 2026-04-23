// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class OverridableValueTests
{
    [Fact]
    public void GetEffectiveValue_NotOverridden_ReturnsDefault()
    {
        var ov = new OverridableValue<string>();
        ov.GetEffectiveValue("default").Should().Be("default");
    }

    [Fact]
    public void GetEffectiveValue_Overridden_ReturnsOverriddenValue()
    {
        var ov = new OverridableValue<string> { IsOverridden = true, Value = "custom" };
        ov.GetEffectiveValue("default").Should().Be("custom");
    }

    [Fact]
    public void GetEffectiveValue_OverriddenWithNull_ReturnsDefault()
    {
        var ov = new OverridableValue<string> { IsOverridden = true, Value = null };
        ov.GetEffectiveValue("default").Should().Be("default");
    }

    [Fact]
    public void GetEffectiveValue_IntNotOverridden_ReturnsDefault()
    {
        var ov = new OverridableValue<int>();
        ov.GetEffectiveValue(42).Should().Be(42);
    }

    [Fact]
    public void GetEffectiveValue_IntOverridden_ReturnsOverriddenValue()
    {
        var ov = new OverridableValue<int> { IsOverridden = true, Value = 99 };
        ov.GetEffectiveValue(42).Should().Be(99);
    }

    [Fact]
    public void GetEffectiveValue_BoolOverridden_ReturnsOverriddenValue()
    {
        var ov = new OverridableValue<bool> { IsOverridden = true, Value = false };
        ov.GetEffectiveValue(true).Should().BeFalse();
    }
}
