using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DefaultIntrinsicTypePolicyTests
    {
        public static IEnumerable<TestCaseData> Cases ()
        {
            yield return new TestCaseData(typeof(PublicSerializableClass), true).SetName("Intrinsic_PublicSerializableClass_OK");
            yield return new TestCaseData(typeof(SerializableStruct), true).SetName("Intrinsic_ValueTypeStruct_OK");
            yield return new TestCaseData(typeof(Outer.NestedPublicSerializableClass), true).SetName("Intrinsic_NestedPublic_OK");
            yield return new TestCaseData(Outer.NestedPrivateType, true).SetName("Intrinsic_NestedPrivate_OK");

            yield return new TestCaseData(typeof(InternalSerializableClass), false).SetName("Intrinsic_Internal_NG");
            yield return new TestCaseData(typeof(AbstractSerializableClass), false).SetName("Intrinsic_Abstract_NG");
            yield return new TestCaseData(typeof(NonSerializableClass), false).SetName("Intrinsic_NoSerializableAttribute_NG");
            yield return new TestCaseData(typeof(HiddenSerializableClass), false).SetName("Intrinsic_HideInTypeMenu_NG");
            yield return new TestCaseData(typeof(UnityObjectDerived), false).SetName("Intrinsic_UnityObjectDerived_NG");

            yield return new TestCaseData(typeof(GenericCandidate<int>), false).SetName("Intrinsic_GenericClosed_NG");
        }

        [TestCaseSource(nameof(Cases))]
        public void IsAllowed_MatchesExpected (Type type, bool expected)
        {
            bool actual = DefaultIntrinsicTypePolicy.Instance.IsAllowed(type, false);
            Assert.That(actual, Is.EqualTo(expected), type.FullName);
        }
    }
}
