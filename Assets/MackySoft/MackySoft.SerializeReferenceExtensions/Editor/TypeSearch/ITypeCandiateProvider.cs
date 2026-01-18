using System;
using System.Collections.Generic;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public interface ITypeCandiateProvider
	{
		IEnumerable<Type> GetTypeCandidates (Type baseType);
    }
}