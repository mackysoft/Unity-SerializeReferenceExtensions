#if UNITY_2019_3_OR_NEWER

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA2235 // Mark all non-serializable fields

using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Example {

	public class Example : MonoBehaviour {

		// The type that implements ICommand will be displayed in the popup.
		[SerializeReference, SubclassSelector]
		ICommand m_Command;

		// Collection support
		[SerializeReference, SubclassSelector]
		ICommand[] m_Commands = Array.Empty<ICommand>();

		void Start () {
			m_Command?.Execute();

			foreach (ICommand command in m_Commands) {
				command?.Execute();
			}
		}

		// Nested type support
		[Serializable]
		public class NestedCommand : ICommand {
			public void Execute () {
				Debug.Log("Execute NestedCommand");
			}
		}

	}

	public interface ICommand {
		void Execute ();
	}

	[Serializable]
	public class DebugCommand : ICommand {
		[SerializeField]
		string m_Message;
		public void Execute () {
			Debug.Log(m_Message);
		}
	}

	[Serializable]
	public class InstantiateCommand : ICommand {

		[SerializeField]
		GameObject m_Prefab;

		public void Execute () {
			UnityEngine.Object.Instantiate(m_Prefab,Vector3.zero,Quaternion.identity);
		}
	}

	// Menu override support
	[AddTypeMenu("Example/Add Type Menu Command")]
	[Serializable]
	public class AddTypeMenuCommand : ICommand {
		public void Execute () {
			Debug.Log("Execute AddTypeMenuCommand");
		}
	}

	[Serializable]
	public struct StructCommand : ICommand {
		public void Execute () {
			Debug.Log("Execute StructCommand");
		}
	}

}
#endif