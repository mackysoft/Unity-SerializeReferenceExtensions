using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Example.Editor {

	[CustomEditor(typeof(Example))]
	public class ExampleEditor : UnityEditor.Editor {

		// Enabling the custom editor slowdown rendering performance.
		public override void OnInspectorGUI () {
			base.OnInspectorGUI();
		}
	}
}