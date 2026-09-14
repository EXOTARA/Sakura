using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Hardware;

namespace Nexo.Core.Tests;

public sealed class AdaptiveEnginePolicyTests
{
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(HardwareCapabilityTier.Basic)]
    [InlineData(HardwareCapabilityTier.Standard)]
    [InlineData(HardwareCapabilityTier.Accelerated)]
    [InlineData(HardwareCapabilityTier.HighPerformance)]
    public void Automatic_WithSingleLowCostEngine_RecommendsItAtEveryTier(HardwareCapabilityTier tier)
    {
        var profile = CreateProfile(tier, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("stt.test", EngineCategory.SpeechToText);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.SpeechToText);
        Assert.Equal(descriptor.Id, recommendation.RecommendedEngineId);
        Assert.Equal(tier, plan.Tier);
    }

    [Fact]
    public void SingleRegisteredEngine_IsRecommendedWhenCompatible()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("tts.test", EngineCategory.TextToSpeech);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Balanced, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.TextToSpeech);
        Assert.Equal(descriptor.Id, recommendation.RecommendedEngineId);
        Assert.Empty(recommendation.CompatibleAlternatives);
        Assert.Empty(recommendation.IncompatibleEngines);
    }

    [Theory]
    [InlineData(HardwareCapabilityTier.Basic)]
    [InlineData(HardwareCapabilityTier.Standard)]
    [InlineData(HardwareCapabilityTier.Accelerated)]
    [InlineData(HardwareCapabilityTier.HighPerformance)]
    public void Eco_AcrossTiers_AlwaysPicksLowestCostProfile(HardwareCapabilityTier tier)
    {
        var profile = CreateProfile(tier, HardwareDataConfidence.Known);
        var descriptors = new[] { LightDescriptor(), MediumDescriptor(), HeavyDescriptor() };

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Eco, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal("light", recommendation.RecommendedEngineId!.Value.Value);
    }

    [Theory]
    [InlineData(HardwareCapabilityTier.Basic, "light")]
    [InlineData(HardwareCapabilityTier.Standard, "medium")]
    [InlineData(HardwareCapabilityTier.Accelerated, "medium")]
    [InlineData(HardwareCapabilityTier.HighPerformance, "medium")]
    public void Balanced_AcrossTiers_PicksClosestToModerateCostProfile(HardwareCapabilityTier tier, string expectedId)
    {
        var profile = CreateProfile(tier, HardwareDataConfidence.Known);
        var descriptors = new[] { LightDescriptor(), MediumDescriptor(), HeavyDescriptor() };

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Balanced, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal(expectedId, recommendation.RecommendedEngineId!.Value.Value);
    }

    [Theory]
    [InlineData(HardwareCapabilityTier.Basic, "light")]
    [InlineData(HardwareCapabilityTier.Standard, "medium")]
    [InlineData(HardwareCapabilityTier.Accelerated, "heavy")]
    [InlineData(HardwareCapabilityTier.HighPerformance, "heavy")]
    public void Maximum_AcrossTiers_PicksHighestCompatibleCostProfile_NeverIncompatible(
        HardwareCapabilityTier tier, string expectedId)
    {
        var profile = CreateProfile(tier, HardwareDataConfidence.Known);
        var descriptors = new[] { LightDescriptor(), MediumDescriptor(), HeavyDescriptor() };

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Maximum, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal(expectedId, recommendation.RecommendedEngineId!.Value.Value);
        Assert.DoesNotContain(recommendation.RecommendedEngineId!.Value, recommendation.IncompatibleEngines.Select(c => c.EngineId));
    }

    [Fact]
    public void UnknownHardwareConfidence_AddsGeneralWarningButStillProducesAPlan()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Unknown);
        var descriptor = CreateDescriptor("stt.test", EngineCategory.SpeechToText);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        Assert.Contains(plan.GeneralWarnings, w => w.Contains("parcial"));
        Assert.NotEmpty(plan.Recommendations);
    }

    [Fact]
    public void EstimatedConfidence_AddsGeneralWarning()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Accelerated, HardwareDataConfidence.Estimated);
        var descriptor = CreateDescriptor("stt.test", EngineCategory.SpeechToText);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        Assert.Contains(plan.GeneralWarnings, w => w.Contains("parcial"));
    }

    [Fact]
    public void KnownConfidence_DoesNotAddThePartialDataWarning()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Accelerated, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("stt.test", EngineCategory.SpeechToText);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        Assert.DoesNotContain(plan.GeneralWarnings, w => w.Contains("parcial"));
    }

    [Fact]
    public void RecommendedEngine_NotAvailable_StillRecommendedButWarns()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("stt.test", EngineCategory.SpeechToText);
        var state = new EngineRuntimeState(descriptor.Id, IsAvailable: false, IsConfigured: null, IsActive: null, Detail: null);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, new[] { state }, FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.SpeechToText);
        Assert.Equal(descriptor.Id, recommendation.RecommendedEngineId);
        Assert.Contains(recommendation.Warnings, w => w.Contains("no está disponible"));
    }

    [Fact]
    public void Warnings_AreShortAndDoNotRepeatTheEngineName()
    {
        // 2026-09-14 — la tarjeta ya nombra el motor; las frases que empezaban por su nombre lo
        // repetían cuatro veces seguidas. Y si falta descargarlo, no se dice además que no está.
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("stt.motor-largo", EngineCategory.SpeechToText, requiresRestart: true, requiresDownload: true);
        var state = new EngineRuntimeState(descriptor.Id, IsAvailable: false, IsConfigured: null, IsActive: null, Detail: null);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, new[] { state }, FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.SpeechToText);
        Assert.Equal(["Falta descargarlo.", "Se aplica al reiniciar Sakura."], recommendation.Warnings);
        Assert.All(recommendation.Reasons.Concat(recommendation.Warnings), text => Assert.DoesNotContain("stt.motor-largo", text));
        Assert.DoesNotContain("Automático", plan.Summary);
    }

    [Fact]
    public void AvailableButIncompatibleEngine_AppearsInIncompatibleListNotRecommended()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Basic, HardwareDataConfidence.Known);
        var heavy = HeavyDescriptor();
        var state = new EngineRuntimeState(heavy.Id, IsAvailable: true, IsConfigured: null, IsActive: null, Detail: null);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { heavy }, new[] { state }, FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Null(recommendation.RecommendedEngineId);
        Assert.Contains(recommendation.IncompatibleEngines, c => c.EngineId == heavy.Id && !c.IsCompatible);
    }

    [Fact]
    public void ActiveEngine_DifferentFromRecommended_MarksRecommendationOnly()
    {
        var profile = CreateProfile(HardwareCapabilityTier.HighPerformance, HardwareDataConfidence.Known);
        var light = LightDescriptor();
        var heavy = HeavyDescriptor();
        var activeState = new EngineRuntimeState(light.Id, IsAvailable: true, IsConfigured: true, IsActive: true, Detail: null);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Maximum, new[] { light, heavy }, new[] { activeState }, FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal(light.Id, recommendation.ActiveEngineId);
        Assert.Equal(heavy.Id, recommendation.RecommendedEngineId);
        Assert.True(recommendation.IsRecommendationOnly);
    }

    [Fact]
    public void ConfiguredEngine_NotActive_ReflectsBothIndependently()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor("llm.test", EngineCategory.LocalLanguageModel);
        var state = new EngineRuntimeState(descriptor.Id, IsAvailable: true, IsConfigured: true, IsActive: false, Detail: null);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, new[] { state }, FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal(descriptor.Id, recommendation.ConfiguredEngineId);
        Assert.Null(recommendation.ActiveEngineId);
    }

    [Fact]
    public void NoEngineRegisteredForACategory_ProducesEmptyRecommendationWithWarning()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var sttOnly = CreateDescriptor("stt.test", EngineCategory.SpeechToText);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { sttOnly }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var wakeWordRecommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.WakeWord);
        Assert.Null(wakeWordRecommendation.RecommendedEngineId);
        Assert.Contains(wakeWordRecommendation.Warnings, w => w.Contains("No hay motores registrados"));
    }

    [Fact]
    public void Recommendations_AreAlwaysInFixedCategoryOrder()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Standard, HardwareDataConfidence.Known);
        var descriptors = new[]
        {
            CreateDescriptor("llm.test", EngineCategory.LocalLanguageModel),
            CreateDescriptor("tts.test", EngineCategory.TextToSpeech),
            CreateDescriptor("wake.test", EngineCategory.WakeWord),
            CreateDescriptor("stt.test", EngineCategory.SpeechToText)
        };

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        Assert.Equal(
            new[]
            {
                EngineCategory.SpeechToText,
                EngineCategory.WakeWord,
                EngineCategory.TextToSpeech,
                EngineCategory.LocalLanguageModel
            },
            plan.Recommendations.Select(r => r.Category));
    }

    [Fact]
    public void SameInput_ProducesIdenticalPlan()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Accelerated, HardwareDataConfidence.Known);
        var descriptors = new[] { LightDescriptor(), HeavyDescriptor() };
        var states = new[] { new EngineRuntimeState(descriptors[0].Id, true, true, true, null) };

        var first = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Maximum, descriptors, states, FixedTimestamp);
        var second = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Maximum, descriptors, states, FixedTimestamp);

        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.Tier, second.Tier);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.True(first.GeneralWarnings.SequenceEqual(second.GeneralWarnings));
        Assert.True(first.AvailableTodayChanges.SequenceEqual(second.AvailableTodayChanges));
        Assert.True(first.FutureChanges.SequenceEqual(second.FutureChanges));
        Assert.Equal(first.Recommendations.Count, second.Recommendations.Count);

        for (var i = 0; i < first.Recommendations.Count; i++)
        {
            var a = first.Recommendations[i];
            var b = second.Recommendations[i];
            Assert.Equal(a.Category, b.Category);
            Assert.Equal(a.RecommendedEngineId, b.RecommendedEngineId);
            Assert.Equal(a.ConfiguredEngineId, b.ConfiguredEngineId);
            Assert.Equal(a.ActiveEngineId, b.ActiveEngineId);
            Assert.True(a.Reasons.SequenceEqual(b.Reasons));
            Assert.True(a.Warnings.SequenceEqual(b.Warnings));
        }
    }

    [Fact]
    public void Automatic_UnknownEngineCost_DoesNotBlockCompatibility()
    {
        var profile = CreateProfile(HardwareCapabilityTier.Basic, HardwareDataConfidence.Known);
        var descriptor = CreateDescriptor(
            "llm.unknown-cost",
            EngineCategory.LocalLanguageModel,
            minCost: EngineCostLevel.Unknown,
            recCpu: EngineCostLevel.Unknown,
            recRam: EngineCostLevel.Unknown,
            recGpu: EngineCostLevel.Unknown,
            recEnergy: EngineCostLevel.Unknown);

        var plan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, new[] { descriptor }, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var recommendation = plan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        Assert.Equal(descriptor.Id, recommendation.RecommendedEngineId);
        Assert.Empty(recommendation.IncompatibleEngines);
    }

    [Fact]
    public void Automatic_WithUnknownConfidence_BehavesConservativelyLikeEco()
    {
        var profile = CreateProfile(HardwareCapabilityTier.HighPerformance, HardwareDataConfidence.Unknown);
        var descriptors = new[] { LightDescriptor(), MediumDescriptor(), HeavyDescriptor() };

        var automaticPlan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);
        var ecoPlan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Eco, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var automaticRecommendation = automaticPlan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        var ecoRecommendation = ecoPlan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);

        Assert.Equal(ecoRecommendation.RecommendedEngineId, automaticRecommendation.RecommendedEngineId);
    }

    [Fact]
    public void Automatic_WithKnownConfidence_BehavesLikeBalanced()
    {
        var profile = CreateProfile(HardwareCapabilityTier.HighPerformance, HardwareDataConfidence.Known);
        var descriptors = new[] { LightDescriptor(), MediumDescriptor(), HeavyDescriptor() };

        var automaticPlan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Automatic, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);
        var balancedPlan = AdaptiveEnginePolicy.Evaluate(
            profile, HardwarePerformanceMode.Balanced, descriptors, Array.Empty<EngineRuntimeState>(), FixedTimestamp);

        var automaticRecommendation = automaticPlan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);
        var balancedRecommendation = balancedPlan.Recommendations.Single(r => r.Category == EngineCategory.LocalLanguageModel);

        Assert.Equal(balancedRecommendation.RecommendedEngineId, automaticRecommendation.RecommendedEngineId);
    }

    private static HardwareCapabilityProfile CreateProfile(HardwareCapabilityTier tier, HardwareDataConfidence confidence) =>
        new(
            HardwareCapabilitySnapshot.Empty,
            tier,
            "test summary",
            Array.Empty<HardwareCapabilityReason>(),
            Array.Empty<HardwareCapabilityReason>(),
            Array.Empty<HardwareCapabilityReason>(),
            confidence);

    private static EngineDescriptor LightDescriptor() => CreateDescriptor(
        "light", EngineCategory.LocalLanguageModel,
        recCpu: EngineCostLevel.Low, recRam: EngineCostLevel.Low, recGpu: EngineCostLevel.Low, recEnergy: EngineCostLevel.Low);

    private static EngineDescriptor MediumDescriptor() => CreateDescriptor(
        "medium", EngineCategory.LocalLanguageModel,
        minCost: EngineCostLevel.Moderate,
        recCpu: EngineCostLevel.Moderate, recRam: EngineCostLevel.Moderate, recGpu: EngineCostLevel.Moderate, recEnergy: EngineCostLevel.Moderate);

    private static EngineDescriptor HeavyDescriptor() => CreateDescriptor(
        "heavy", EngineCategory.LocalLanguageModel,
        minCost: EngineCostLevel.High,
        recCpu: EngineCostLevel.High, recRam: EngineCostLevel.High, recGpu: EngineCostLevel.High, recEnergy: EngineCostLevel.High);

    private static EngineDescriptor CreateDescriptor(
        string id,
        EngineCategory category,
        EngineCostLevel minCost = EngineCostLevel.Low,
        EngineCostLevel recCpu = EngineCostLevel.Low,
        EngineCostLevel recRam = EngineCostLevel.Low,
        EngineCostLevel recGpu = EngineCostLevel.Low,
        EngineCostLevel recEnergy = EngineCostLevel.Low,
        bool? isLocal = true,
        bool allowsRuntimeSelection = true,
        bool requiresRestart = false,
        bool requiresDownload = false,
        bool includedWithSakura = true)
    {
        var minimum = new EngineRequirement("mínimo de prueba", minCost, minCost, minCost, minCost);
        var recommended = new EngineRequirement("recomendado de prueba", recCpu, recRam, recGpu, recEnergy);

        return new EngineDescriptor(
            new EngineIdentifier(id),
            id,
            category,
            isLocal,
            minimum,
            recommended,
            Array.Empty<string>(),
            Array.Empty<string>(),
            allowsRuntimeSelection,
            requiresRestart,
            requiresDownload,
            includedWithSakura);
    }
}
