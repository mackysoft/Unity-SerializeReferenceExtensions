using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [TestFixture]
    public sealed class TypeCandidateService_NonGenericTests
    {
        private HashSet<Type> candidates;

        [OneTimeSetUp]
        public void OneTimeSetUp ()
        {
            candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(ITestBase)).ToHashSet();
        }

        public static IEnumerable<TestCaseData> Cases ()
        {
            yield return new TestCaseData(typeof(PublicSerializableClass), true).SetName("Candidates_ITestBase_PublicSerializable_OK");
            yield return new TestCaseData(typeof(SerializableStruct), true).SetName("Candidates_ITestBase_ValueTypeStruct_OK");
            yield return new TestCaseData(typeof(Outer.NestedPublicSerializableClass), true).SetName("Candidates_ITestBase_NestedPublic_OK");
            yield return new TestCaseData(Outer.NestedPrivateType, true).SetName("Candidates_ITestBase_NestedPrivate_OK");

            yield return new TestCaseData(typeof(InternalSerializableClass), false).SetName("Candidates_ITestBase_Internal_NG");
            yield return new TestCaseData(typeof(AbstractSerializableClass), false).SetName("Candidates_ITestBase_Abstract_NG");
            yield return new TestCaseData(typeof(NonSerializableClass), false).SetName("Candidates_ITestBase_NoSerializable_NG");
            yield return new TestCaseData(typeof(HiddenSerializableClass), false).SetName("Candidates_ITestBase_HideInTypeMenu_NG");
            yield return new TestCaseData(typeof(UnityObjectDerived), false).SetName("Candidates_ITestBase_UnityObjectDerived_NG");
            yield return new TestCaseData(typeof(GenericCandidate<int>), false).SetName("Candidates_ITestBase_GenericClosed_NG");
        }

        [TestCaseSource(nameof(Cases))]
        public void CandidatesContainment_IsExpected (Type type, bool expected)
        {
            if (expected)
            {
                Assert.That(candidates, Does.Contain(type), $"Expected to contain: {type.FullName}");
            }
            else
            {
                Assert.That(candidates, !Does.Contain(type), $"Expected NOT to contain: {type.FullName}");
            }
        }

        [Test]
        public void Candidates_HaveNoDuplicates ()
        {
            var list = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(ITestBase)).ToList();
            Assert.That(list.Count, Is.EqualTo(list.Distinct().Count()));
        }
    }
}
