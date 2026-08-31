using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Runtime.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class ContentConfigValidatorTests
    {
        private const string RootPath = "Assets/Game/Content/Config/GameContentConfig.asset";
        private readonly List<Object> _clones = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var clone in _clones) Object.DestroyImmediate(clone);
            _clones.Clear();
        }

        [Test]
        public void BaselineAsset_PassesFullValidation()
        {
            var report = ContentConfigValidator.Validate(LoadRoot());

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
        }

        [Test]
        public void DuplicateStableId_IsReportedWithCatalogPath()
        {
            var root = CloneRootWithCatalog("_resourceCatalog", LoadRoot().ResourceCatalog, out var catalog);
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("_definitions");
            definitions.arraySize++;
            definitions.GetArrayElementAtIndex(definitions.arraySize - 1).FindPropertyRelative("_id").stringValue = "resource.food";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var report = ContentConfigValidator.Validate(root);

            Assert.That(report.Issues.Any(issue => issue.Code == ContentValidationCode.DuplicateId && issue.Path.StartsWith("ResourceCatalog")), Is.True);
        }

        [Test]
        public void NonPlankBuildingUpgrade_IsRejected()
        {
            var root = CloneRootWithCatalog("_buildingCatalog", LoadRoot().BuildingCatalog, out var catalog);
            var serialized = new SerializedObject(catalog);
            var firstUpgrade = serialized.FindProperty("_definitions").GetArrayElementAtIndex(0)
                .FindPropertyRelative("_upgradeLevels").GetArrayElementAtIndex(0);
            firstUpgrade.FindPropertyRelative("_paymentResourceId").stringValue = "resource.stone";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var report = ContentConfigValidator.Validate(root);

            Assert.That(report.Issues.Any(issue => issue.Code == ContentValidationCode.InvalidUpgradeCost), Is.True);
        }

        [Test]
        public void InitialHandWithoutRequiredGuarantees_IsRejected()
        {
            var root = CloneRootWithCatalog("_battlefieldCatalog", LoadRoot().BattlefieldCatalog, out var catalog);
            var serialized = new SerializedObject(catalog);
            var hand = serialized.FindProperty("_definitions").GetArrayElementAtIndex(0).FindPropertyRelative("_initialHand");
            hand.FindPropertyRelative("_guaranteedCardIds").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var report = ContentConfigValidator.Validate(root);

            Assert.That(report.Issues.Any(issue => issue.Code == ContentValidationCode.InvalidInitialHand), Is.True);
        }

        private GameContentConfig LoadRoot()
        {
            return AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath)
                ?? throw new AssertionException("Missing baseline GameContentConfig asset.");
        }

        private GameContentConfig CloneRootWithCatalog<T>(string fieldName, T source, out T clone)
            where T : ScriptableObject
        {
            var root = Object.Instantiate(LoadRoot());
            clone = Object.Instantiate(source);
            _clones.Add(root);
            _clones.Add(clone);
            var serialized = new SerializedObject(root);
            serialized.FindProperty(fieldName).objectReferenceValue = clone;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }
}
