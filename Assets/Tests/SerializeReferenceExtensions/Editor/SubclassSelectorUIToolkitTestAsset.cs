using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorUIToolkitTestAsset : ScriptableObject
    {
        [SerializeReference, SubclassSelector] public INode reference;
        [SerializeReference] public INode nativeReference;
        [ProbeDecorator("Selector heading"), SerializeReference, SubclassSelector] public INode decoratedReference;
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] public INode described;
        [SerializeReference, SubclassSelector] public INode[] array = Array.Empty<INode>();
        [SerializeReference, SubclassSelector] public List<INode> list = new List<INode>();
        [SerializeReference, SubclassSelector, NonReorderable] public INode[] simpleArray = Array.Empty<INode>();
        [SerializeReference, SubclassSelector, NonReorderable] public List<INode> simpleList = new List<INode>();
        [SerializeReference] public INode[] nativeArray = Array.Empty<INode>();
        [SerializeReference] public List<INode> nativeList = new List<INode>();
        [SerializeReference, NonReorderable] public INode[] nativeSimpleArray = Array.Empty<INode>();
        [SerializeReference, NonReorderable] public List<INode> nativeSimpleList = new List<INode>();
        public enum LayoutKind { PlainNode, Alternate }
        public LayoutKind nativePopupReference;
        public LayoutKind[] nativePopupArray = Array.Empty<LayoutKind>();
        public List<LayoutKind> nativePopupList = new List<LayoutKind>();
        [NonReorderable] public LayoutKind[] nativePopupSimpleArray = Array.Empty<LayoutKind>();
        [NonReorderable] public List<LayoutKind> nativePopupSimpleList = new List<LayoutKind>();
        [SerializeReference, NativeOnGUI(order = -1), SubclassSelector] public INode nativeOnGUIReference;
        [SerializeReference, NativeOnGUI(order = -1), SubclassSelector] public INode[] nativeOnGUIArray = Array.Empty<INode>();
        [SerializeReference, NativeOnGUI(order = -1), SubclassSelector, NonReorderable] public INode[] nativeOnGUISimpleArray = Array.Empty<INode>();
        [SerializeField] public Parent parent = new Parent();

        public interface INode { }

        [Serializable]
        public sealed class NativeHeaderReference : INode
        {
            public int value;
        }

        public sealed class NativeOnGUIAttribute : PropertyAttribute { }

        [CustomPropertyDrawer(typeof(NativeOnGUIAttribute))]
        public sealed class NativeOnGUIDrawer : PropertyDrawer
        {
            public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
                => EditorGUI.GetPropertyHeight(property, label, true);

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
                => EditorGUI.PropertyField(position, property, label, true);
        }

        [Serializable]
        public sealed class PlainNode : INode
        {
            public int value;

            public override string ToString () => $"Value: {value}";
        }

        [Serializable]
        public sealed class BooleanNode : INode
        {
            public bool value;
        }

        [Serializable]
        public sealed class AlternateNode : INode
        {
            public int other;
        }

        [Serializable]
        public sealed class RelativeNode : INode
        {
            public int value;

            public override string ToString () => $"Relative value: {value} ({RuntimeHelpers.GetHashCode(this)})";
        }

        [Serializable]
        public sealed class TrackedNode : INode
        {
            public int value;
            public string marker;
        }

        [Serializable]
        public sealed class UnannotatedChildNode : INode
        {
            public int nativeValue = 811;
            [SerializeReference] public INode child;
        }

        [Serializable]
        public class BaseDrawerNode : INode
        {
            public int marker;
        }

        [Serializable]
        public sealed class DerivedDrawerNode : BaseDrawerNode { }

        [CustomPropertyDrawer(typeof(BaseDrawerNode))]
        public sealed class BaseDrawerNodeDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
                => new IntegerField("Inherited drawer value") { bindingPath = nameof(BaseDrawerNode.marker) };
        }

        [Serializable]
        public sealed class FoldoutStateNode : INode
        {
            public FoldoutStateDetails details = new FoldoutStateDetails();
        }

        [Serializable]
        public sealed class FoldoutStateDetails
        {
            public bool value;
            public int[] numbers = { 11, 13, 17 };
        }

        [Serializable]
        public sealed class BooleanGroupNode : INode
        {
            public BooleanGroupValue details = new BooleanGroupValue();
        }

        [Serializable]
        public sealed class BooleanGroupValue
        {
            public bool value;
        }

        [Serializable]
        public sealed class CollectionSelectionNode : INode
        {
            public int[] numbers = { 11, 13, 17 };
            public List<int> list = new List<int> { 11, 13, 17 };
        }

        [Serializable]
        public sealed class CompositeChildGraphNode : INode
        {
            public CompositeChildGraph details = new CompositeChildGraph();
        }

        [Serializable]
        public sealed class CollectionLayoutNode : INode
        {
            public CollectionLayoutDetails details = new CollectionLayoutDetails();
        }

        [Serializable]
        public sealed class CollectionLayoutDetails
        {
            public int nativeValue = 907;
            public CollectionLayoutSlot[] array = { new CollectionLayoutSlot() };
            public List<CollectionLayoutSlot> list = new List<CollectionLayoutSlot> { new CollectionLayoutSlot() };
            [NonReorderable] public CollectionLayoutSlot[] simpleArray = { new CollectionLayoutSlot() };
            [NonReorderable] public List<CollectionLayoutSlot> simpleList = new List<CollectionLayoutSlot> { new CollectionLayoutSlot() };
        }

        [Serializable]
        public sealed class CollectionLayoutSlot
        {
            public int marker = 919;
        }

        [Serializable]
        public sealed class DrawerCollectionLayoutNode : INode
        {
            public FoldoutDrawerRow[] array = { new FoldoutDrawerRow() };
            [NonReorderable] public FoldoutDrawerRow[] simpleArray = { new FoldoutDrawerRow() };
            [NonReorderable] public List<FoldoutDrawerRow> simpleList = new List<FoldoutDrawerRow> { new FoldoutDrawerRow() };
            public int[] numbers = { 11, 13 };
            [SerializeReference, NonReorderable] public INode[] mixed =
                { new PlainNode { value = 17 }, new FoldoutDrawerRow { marker = 19 } };
        }

        [Serializable]
        public sealed class FoldoutDrawerRow : INode
        {
            public int marker = 941;
        }

        [CustomPropertyDrawer(typeof(FoldoutDrawerRow))]
        public sealed class FoldoutDrawerRowDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                var foldout = new Foldout { text = property.displayName, bindingPath = property.propertyPath };
                foldout.Add(new IntegerField("Marker") { bindingPath = nameof(FoldoutDrawerRow.marker) });
                return foldout;
            }
        }

        [Serializable]
        public sealed class CompositeChildGraph
        {
            public int nativeValue = 907;
            public ChildSlot[] array = { new ChildSlot() };
            public List<ChildSlot> list = new List<ChildSlot> { new ChildSlot() };
        }

        [Serializable]
        public sealed class ChildSlot
        {
            public int marker;
            [SerializeReference] public INode child;
        }

        [Serializable]
        public sealed class DecoratedSelectorNode : INode
        {
            [ProbeDecorator("Nested selector heading"), SerializeReference, SubclassSelector] public INode child;
        }

        [Serializable]
        public sealed class DecoratedCollectionNode : INode
        {
            [ProbeDecorator("Array selector heading"), SerializeReference, SubclassSelector] public INode[] array = Array.Empty<INode>();
            [ProbeDecorator("List selector heading"), SerializeReference, SubclassSelector] public List<INode> list = new List<INode>();
        }

        [Serializable]
        public sealed class DecoratedUIToolkitNode : INode
        {
            [ProbeDecorator("UITK type heading")] public RelativeNode relative = new RelativeNode { value = 409 };
            [ProbeDecorator("UITK generic heading")] public GenericNode<int> generic = new GenericNode<int> { value = 419 };
            [ProbeDecorator("UITK attribute heading"), UIToolkitValue] public int annotated = 421;
        }

        public sealed class UIToolkitValueAttribute : PropertyAttribute { }

        [CustomPropertyDrawer(typeof(UIToolkitValueAttribute))]
        public sealed class UIToolkitValueDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                return new IntegerField("UITK attribute value") { bindingPath = property.propertyPath };
            }
        }

#if UNITY_2022_2_OR_NEWER
        [Serializable]
        public sealed class ChainedChildNode : INode
        {
            public int nativeValue = 811;
            [SerializeReference, DrawerChain("child argument")] public INode child;
        }

        public sealed class DrawerChainAttribute : PropertyAttribute
        {
            public string Token { get; }
            public DrawerChainAttribute (string token) => Token = token;
        }

        [CustomPropertyDrawer(typeof(DrawerChainAttribute))]
        public sealed class DrawerChainDrawer : PropertyDrawer
        {
            public static readonly List<string> ReferenceChanges = new List<string>();
            public static readonly List<string> ValueChanges = new List<string>();

            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                long registeredId = property.managedReferenceId;
                var result = new VisualElement();
                result.Add(new Label("Chain metadata: " + ((DrawerChainAttribute)attribute).Token + "/" + fieldInfo?.Name));
                var status = new Label();
                result.Add(status);
                void Record (SerializedProperty current)
                {
                    status.text = "Chain tracked: " + current.intValue;
                }
                using (SerializedProperty value = property.FindPropertyRelative(nameof(ChainedTrackedNode.value)))
                {
                    Record(value);
                    result.TrackPropertyValue(value, current =>
                    {
                        ValueChanges.Add(current.propertyPath + "=" + current.intValue);
                        Record(current);
                    });
                }
                result.TrackPropertyValue(property, current => ReferenceChanges.Add(current.propertyPath + "=" + registeredId
                    + "->" + current.managedReferenceId + "/mixed=" + current.hasMultipleDifferentValues));
                result.Add(new PropertyField(property, "Chained input"));
                return result;
            }
        }

        [Serializable]
        public sealed class ChainedTrackedNode : INode
        {
            public int value;
            public string marker;
            public bool showChild;
            public bool moveOnEdit;
            [SerializeReference] public INode next;
        }

        [CustomPropertyDrawer(typeof(ChainedTrackedNode))]
        public sealed class ChainedTrackedNodeDrawer : PropertyDrawer
        {
            public static readonly List<string> ReferenceChanges = new List<string>();
            public static readonly List<string> ValueChanges = new List<string>();

            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                SerializedObject source = property.serializedObject;
                string path = property.propertyPath;
                long registeredId = property.managedReferenceId;
                var result = new VisualElement();
                result.Add(new Label("Type metadata: " + fieldInfo?.Name + "/" + preferredLabel));
                result.Add(new IntegerField("Chained value") { bindingPath = nameof(ChainedTrackedNode.value) });
                var status = new Label();
                result.Add(status);
                bool moved = false;
                void Record (SerializedProperty current)
                {
                    using (SerializedProperty move = source.FindProperty(path + ".moveOnEdit"))
                    {
                        // A custom drawer may move its row during its own value notification.
                        // Keep the row in the same panel, with no rebinding or focus repair.
                        if (!moved && current.intValue == 9 && move?.boolValue == true)
                        {
                            PropertyField row = result.GetFirstAncestorOfType<PropertyField>();
                            if (row?.parent != null && row.parent.IndexOf(row) < row.parent.childCount - 1)
                            {
                                row.BringToFront();
                                moved = true;
                            }
                        }
                        status.text = "Type tracked: " + current.intValue + (moved ? "; row moved" : "");
                    }
                }
                using (SerializedProperty value = property.FindPropertyRelative(nameof(ChainedTrackedNode.value)))
                {
                    Record(value);
                    result.TrackPropertyValue(value, current =>
                    {
                        ValueChanges.Add(current.propertyPath + "=" + current.intValue);
                        Record(current);
                    });
                }
                result.TrackPropertyValue(property, current => ReferenceChanges.Add(current.propertyPath + "=" + registeredId
                    + "->" + current.managedReferenceId + "/mixed=" + current.hasMultipleDifferentValues));
                var children = new VisualElement();
                result.Add(children);
                void AddChild (bool bind)
                {
                    using (SerializedProperty child = source.FindProperty(path + ".next"))
                    {
                        child.isExpanded = true;
                        var field = new PropertyField(child, "Late child");
                        children.Add(field);
                        children.Add(new Label("After late row"));
                        if (bind)
                        {
                            field.Bind(source);
                        }
                    }
                }
                using (SerializedProperty shown = property.FindPropertyRelative(nameof(ChainedTrackedNode.showChild)))
                {
                    if (shown.boolValue)
                    {
                        AddChild(false);
                    }
                }
                result.Add(new Button(() =>
                {
                    source.Update();
                    using (SerializedProperty shown = source.FindProperty(path + ".showChild"))
                    {
                        shown.boolValue = true;
                        source.ApplyModifiedProperties();
                    }
                    if (children.childCount == 0)
                    {
                        AddChild(true);
                    }
                }) { text = "Show child" });
                return result;
            }
        }

        [Serializable]
        public sealed class ForeignFieldNode : INode { }

        [CustomPropertyDrawer(typeof(ForeignFieldNode))]
        public sealed class ForeignFieldNodeDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                using (SerializedProperty foreign = property.serializedObject.FindProperty(nameof(nativeReference)))
                {
                    return new PropertyField(foreign, "Foreign reference");
                }
            }
        }

        [Serializable]
        public sealed class SharedSelectorsNode : INode
        {
            [SerializeReference, SubclassSelector] public INode left;
            [SerializeReference, SubclassSelector] public INode right;
        }

        [CustomPropertyDrawer(typeof(SharedSelectorsNode))]
        public sealed class SharedSelectorsNodeDrawer : PropertyDrawer
        {
            public static bool RemovedLiveSelectorOnTypeChange;

            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                string type = property.managedReferenceFullTypename;
                SerializedObject source = property.serializedObject;
                string path = property.propertyPath;
                var result = new VisualElement();
                var left = new PropertyField(property.FindPropertyRelative(nameof(SharedSelectorsNode.left)), "Left selector");
                var right = new PropertyField(property.FindPropertyRelative(nameof(SharedSelectorsNode.right)), "Right selector");
                result.Add(left);
                result.Add(right);
                result.Add(new Button(() => left.RemoveFromHierarchy()) { text = "Hide left" });
                // Removing the final selector during a user notification must leave the
                // still-live native parent able to display its replacement subtype.
                bool tracking = false;
                result.RegisterCallback<AttachToPanelEvent>(evt =>
                {
                    if (tracking)
                    {
                        return;
                    }
                    tracking = true;
                    // Attach runs inside native Add, before Reset registers its tracker.
                    // Track while on-panel so this user's callback is registered first.
                    using (SerializedProperty tracked = source.FindProperty(path))
                    {
                        result.TrackPropertyValue(tracked, current =>
                        {
                            if (current.managedReferenceFullTypename != type)
                            {
                                RemovedLiveSelectorOnTypeChange = right.panel != null;
                                left.RemoveFromHierarchy();
                                right.RemoveFromHierarchy();
                            }
                        });
                    }
                });
                return result;
            }
        }
#endif

        [CustomPropertyDrawer(typeof(TrackedNode))]
        public sealed class TrackedNodeDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                var result = new VisualElement();
                result.Add(new IntegerField("Tracked value") { bindingPath = nameof(TrackedNode.value) });
                using (SerializedProperty value = property.FindPropertyRelative(nameof(TrackedNode.value)))
                {
                    var status = new Label("Tracked value: " + value.intValue);
                    result.Add(status);
                    result.TrackPropertyValue(value, current => status.text = "Tracked value: " + current.intValue);
                }
                return result;
            }
        }

        [Serializable]
        public class DecoratedNodeBase : INode
        {
            [SerializeField, ProbeDecorator("Inherited heading")] private int decoratedValue = 211;
        }

        [Serializable]
        public sealed class DecoratedNode : DecoratedNodeBase
        {
            public int nativeValue = 223;
            public RelativeNode relative = new RelativeNode { value = 227 };
            public GenericNode<int> generic = new GenericNode<int> { value = 279 };
        }

        [Serializable]
        public sealed class GenericNode<T>
        {
            public int value;
        }

        [CustomPropertyDrawer(typeof(GenericNode<>))]
        public sealed class GenericNodeDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                return new IntegerField("Generic value") { bindingPath = "value" };
            }
        }

        public class BaseProbeDecoratorAttribute : PropertyAttribute
        {
            public string Heading { get; }

#if UNITY_6000_0_OR_NEWER
            // Unity 6 requires decorators that belong to a collection to opt in.
            public BaseProbeDecoratorAttribute (string heading) : base(applyToCollection: true) => Heading = heading;
#else
            public BaseProbeDecoratorAttribute (string heading) => Heading = heading;
#endif
        }

        public sealed class ProbeDecoratorAttribute : BaseProbeDecoratorAttribute
        {
            public ProbeDecoratorAttribute (string heading) : base(heading) { }
        }

        [CustomPropertyDrawer(typeof(BaseProbeDecoratorAttribute), true)]
        public sealed class BaseProbePropertyDrawer : PropertyDrawer
        {
            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.LabelField(position, "The exact decorator must take precedence.");
            }
        }

        [CustomPropertyDrawer(typeof(ProbeDecoratorAttribute))]
        public sealed class ProbeDecoratorDrawer : DecoratorDrawer
        {
            public static int DrawCount { get; private set; }
            public static string LastHeading { get; private set; }
            public static readonly HashSet<string> DrawnHeadings = new HashSet<string>();

            public override float GetHeight () => EditorGUIUtility.singleLineHeight;

            public override void OnGUI (Rect position)
            {
                DrawCount++;
                LastHeading = ((ProbeDecoratorAttribute)attribute).Heading;
                DrawnHeadings.Add(LastHeading);
                EditorGUI.LabelField(position, LastHeading);
            }
        }

        [Serializable]
        public sealed class NestedDecoratedNode : INode
        {
            public int nativeValue = 251;
            public List<DecoratedDetails> details = new List<DecoratedDetails>();
            [SerializeReference] public INode unannotated;
            [SerializeReference, NullReference] public INode withDrawer;
        }

        public sealed class NullReferenceAttribute : PropertyAttribute { }

        [CustomPropertyDrawer(typeof(NullReferenceAttribute))]
        public sealed class NullReferenceDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
                => new Label(property.managedReferenceValue == null ? "Null reference drawer" : "Populated reference drawer");
        }

        [Serializable]
        public sealed class DecoratedDetails
        {
            [ProbeDecorator("Nested heading")] public int value = 257;
            [ProbeDecorator("Nested UITK heading")] public RelativeNode relative = new RelativeNode { value = 521 };
            [SerializeReference] public INode reference;
        }

        [Serializable]
        public sealed class CompositeCollectionsNode : INode
        {
            public DecoratedDetails[] array = Array.Empty<DecoratedDetails>();
            public List<DecoratedDetails> list = new List<DecoratedDetails>();
        }

        [Serializable]
        public sealed class TrackedCompositeNode : INode
        {
            public TrackedDetails details = new TrackedDetails();
        }

        [Serializable]
        public sealed class TrackedDetails
        {
            public int value = 751;
        }

        [CustomPropertyDrawer(typeof(TrackedDetails))]
        public sealed class TrackedDetailsDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                var result = new Foldout { text = "Tracked details", bindingPath = property.propertyPath };
                using (SerializedProperty value = property.FindPropertyRelative("value"))
                {
                    var status = new Label("Detail value: " + value.intValue);
                    result.Add(status);
                    status.TrackPropertyValue(value, current => status.text = "Detail value: " + current.intValue);
                }
                return result;
            }
        }

#if UNITY_2022_2_OR_NEWER
        [Serializable]
        public sealed class UIToolkitDecoratedCompositeNode : INode
        {
            [UIToolkitHeading("UITK group heading")] public DecoratedDetails details = new DecoratedDetails();
            [UIToolkitHeading("UITK collection heading")] public List<DecoratedDetails> rows = new List<DecoratedDetails> { new DecoratedDetails() };
        }

        public sealed class UIToolkitHeadingAttribute : PropertyAttribute
        {
            public string Heading { get; }
#if UNITY_6000_0_OR_NEWER
            public UIToolkitHeadingAttribute (string heading) : base(applyToCollection: true) => Heading = heading;
#else
            public UIToolkitHeadingAttribute (string heading) => Heading = heading;
#endif
        }

        [CustomPropertyDrawer(typeof(UIToolkitHeadingAttribute))]
        public sealed class UIToolkitHeadingDrawer : DecoratorDrawer
        {
            public override VisualElement CreatePropertyGUI () => new Label(((UIToolkitHeadingAttribute)attribute).Heading);
        }
#endif

        [Serializable]
        public sealed class RuntimeDecoratedNode : INode
        {
            [ProbeDecorator("Runtime heading")] public int value = 263;
            public RelativeNode relative = new RelativeNode { value = 523 };
        }

        [Serializable]
        public sealed class DeepRuntimeDecoratedNode : INode
        {
            [ProbeDecorator("Deep runtime heading")] public int value = 283;
            public RelativeNode relative = new RelativeNode { value = 541 };
        }

        [Serializable]
        public class ObservedNodeBase : INode
        {
            public int value;
        }

        [Serializable]
        public sealed class ObservedNode : ObservedNodeBase { }

        [CustomPropertyDrawer(typeof(ObservedNodeBase), true)]
        public sealed class ObservedBaseDrawer : PropertyDrawer
        {
            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.LabelField(position, "The exact subtype drawer must take precedence.");
            }
        }

        [CustomPropertyDrawer(typeof(ObservedNode))]
        public sealed class ObservedNodeDrawer : PropertyDrawer
        {
            public static int? LastValue { get; private set; }
            public static string LastLabel { get; private set; }

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                LastValue = property.FindPropertyRelative(nameof(ObservedNode.value)).intValue;
                LastLabel = label.text;
                EditorGUI.LabelField(position, $"Observed value: {LastValue}");
            }
        }

        [Serializable]
        public sealed class InputLayoutNode : INode
        {
            public int value;
        }

        // Unity supplies the outer Foldout and the same OnGUI-only child drawer.
        [Serializable]
        public sealed class NativeInputLayoutNode : INode
        {
            public InputLayoutNode body = new InputLayoutNode();
        }

        [CustomPropertyDrawer(typeof(InputLayoutNode))]
        public sealed class InputLayoutNodeDrawer : PropertyDrawer
        {
            public static Rect InputRect;
            public static Rect LabelRect;
            public static int DrawCount;
            public static readonly Dictionary<string, Rect> InputScreenRects = new Dictionary<string, Rect>();
            public static readonly Dictionary<string, Rect> LabelScreenRects = new Dictionary<string, Rect>();

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                using (SerializedProperty value = property.FindPropertyRelative(nameof(InputLayoutNode.value)))
                {
                    Rect input = EditorGUI.PrefixLabel(position, new GUIContent(value.displayName));
                    using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                    {
                        value.intValue = EditorGUI.IntField(input, value.intValue);
                    }
                    if (Event.current.type == EventType.Repaint)
                    {
                        InputRect = input;
                        LabelRect = EditorGUI.IndentedRect(position);
                        InputScreenRects[property.propertyPath] = new Rect(GUIUtility.GUIToScreenPoint(input.position), input.size);
                        LabelScreenRects[property.propertyPath] = new Rect(GUIUtility.GUIToScreenPoint(LabelRect.position), LabelRect.size);
                        DrawCount++;
                    }
                }
            }
        }

        [Serializable]
        public sealed class InputNode : INode
        {
            public int value;
            public override string ToString () => $"Input: {value}";
        }

        [CustomPropertyDrawer(typeof(InputNode))]
        public sealed class InputNodeDrawer : PropertyDrawer
        {
            public static Rect ScreenRect;

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                if (GUI.Button(position, "Set value to 281"))
                {
                    using (SerializedProperty input = property.FindPropertyRelative(nameof(InputNode.value)))
                    {
                        input.intValue = 281;
                    }
                }
                if (Event.current.type == EventType.Repaint)
                {
                    ScreenRect = new Rect(GUIUtility.GUIToScreenPoint(position.position), position.size);
                }
            }
        }

        [Serializable]
        public sealed class SelfApplyingNode : INode
        {
            public int value;
        }

        [CustomPropertyDrawer(typeof(SelfApplyingNode))]
        public sealed class SelfApplyingNodeDrawer : PropertyDrawer
        {
            public static Action<int> ApplyValue;
            public static bool Repainted;

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                SerializedObject source = property.serializedObject;
                string path = property.propertyPath + "." + nameof(SelfApplyingNode.value);
                ApplyValue = value =>
                {
                    using (SerializedProperty current = source.FindProperty(path))
                    {
                        current.intValue = value;
                        source.ApplyModifiedProperties();
                    }
                };
                if (GUI.Button(position, "Set value to 281"))
                {
                    ApplyValue(281);
                }
                if (Event.current.type == EventType.Repaint)
                {
                    Repainted = true;
                }
            }
        }

        [Serializable]
        public sealed class ContextNode : INode
        {
            public ExposedReference<GameObject> reference;
        }

        [CustomPropertyDrawer(typeof(ContextNode))]
        public sealed class ContextNodeDrawer : PropertyDrawer
        {
            public static UnityEngine.Object LastResolved;

            public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            {
                var node = (ContextNode)property.managedReferenceValue;
                LastResolved = node.reference.Resolve(property.serializedObject.context as IExposedPropertyTable);
                EditorGUI.ObjectField(position, label, LastResolved, typeof(GameObject), true);
            }
        }

        [Serializable]
        public sealed class Parent
        {
            [SerializeReference, SubclassSelector] public INode reference;
        }

        [CustomPropertyDrawer(typeof(RelativeNode))]
        public sealed class RelativeNodeDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                return new IntegerField("Relative value") { bindingPath = nameof(RelativeNode.value) };
            }
        }
    }
}
