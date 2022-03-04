using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Example.Editor {

	// Enabling the custom editor slowdown rendering performance.
	//[CustomEditor(typeof(Example))]
	public class ExampleEditor : UnityEditor.Editor {

		public override void OnInspectorGUI () {
			base.OnInspectorGUI();
		}
	}
}