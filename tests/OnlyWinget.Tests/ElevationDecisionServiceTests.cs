// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class ElevationDecisionServiceTests
{
    [Fact]
    public void Decide_ReturnsElevationProhibited_WhenManifestProhibitsElevation()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: "elevationProhibited");

        Assert.Equal(ElevationMode.ElevationProhibited, mode);
    }

    [Fact]
    public void Decide_ReturnsElevationProhibited_WhenManifestProhibitsElevation_EvenIfAlreadyElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: true,
            scope: "machine",
            manifestElevationRequirement: "elevationProhibited");

        Assert.Equal(ElevationMode.ElevationProhibited, mode);
    }

    [Fact]
    public void Decide_ReturnsSelfElevatingPossible_WhenInstallerSelfElevates()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: "elevatesSelf");

        Assert.Equal(ElevationMode.SelfElevatingPossible, mode);
    }

    [Fact]
    public void Decide_ReturnsElevatedRequired_WhenManifestRequiresElevation_AndProcessIsNotElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: "elevationRequired");

        Assert.Equal(ElevationMode.ElevatedRequired, mode);
    }

    [Fact]
    public void Decide_ReturnsNormal_WhenManifestRequiresElevation_AndProcessIsAlreadyElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: true,
            scope: string.Empty,
            manifestElevationRequirement: "elevationRequired");

        Assert.Equal(ElevationMode.Normal, mode);
    }

    [Fact]
    public void Decide_ReturnsElevatedRequired_WhenMachineScopeAndNotElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: "machine",
            manifestElevationRequirement: string.Empty);

        Assert.Equal(ElevationMode.ElevatedRequired, mode);
    }

    [Fact]
    public void Decide_ReturnsNormal_WhenMachineScopeAndAlreadyElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: true,
            scope: "machine",
            manifestElevationRequirement: string.Empty);

        Assert.Equal(ElevationMode.Normal, mode);
    }

    [Fact]
    public void Decide_ReturnsNormal_WhenUserScopeAndNotElevated()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: "user",
            manifestElevationRequirement: string.Empty);

        Assert.Equal(ElevationMode.Normal, mode);
    }

    [Fact]
    public void Decide_ReturnsNormal_WhenNoScopeAndNoRequirement()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: string.Empty);

        Assert.Equal(ElevationMode.Normal, mode);
    }

    [Fact]
    public void Decide_IsCaseInsensitive_ForManifestRequirement()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: "ELEVATIONREQUIRED");

        Assert.Equal(ElevationMode.ElevatedRequired, mode);
    }

    [Fact]
    public void Decide_IsCaseInsensitive_ForScope()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: "Machine",
            manifestElevationRequirement: string.Empty);

        Assert.Equal(ElevationMode.ElevatedRequired, mode);
    }

    [Fact]
    public void Decide_HandlesNullManifestRequirement_AsEmpty()
    {
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: string.Empty,
            manifestElevationRequirement: null!);

        Assert.Equal(ElevationMode.Normal, mode);
    }

    [Fact]
    public void Decide_ElevationProhibited_TakesPrecedenceOverMachineScope()
    {
        // Even machine-scope should not elevate when manifest says prohibited.
        var mode = ElevationDecisionService.Decide(
            isCurrentProcessElevated: false,
            scope: "machine",
            manifestElevationRequirement: "elevationProhibited");

        Assert.Equal(ElevationMode.ElevationProhibited, mode);
    }
}
