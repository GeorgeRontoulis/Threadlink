namespace Threadlink.Utilities.Editor.Graphs
{
	using System.Collections.Generic;
	using UnityEditor;
	using UnityEditor.Experimental.GraphView;
	using UnityEngine;
	using UnityEngine.UIElements;

	public static class NodeUtitlities
	{
		public static Port CreatePort<PortType, EdgeType>(this Node _, Orientation Orientation, Direction Direction, Port.Capacity Capacity)
		where EdgeType : Edge, new()
		{
			return Port.Create<EdgeType>(Orientation, Direction, Capacity, typeof(PortType));
		}

		public static void SetFontSize(this TextField field, int fontSize)
		{
			field.Q(className: "unity-text-element").style.fontSize = fontSize;
		}
	}

	public interface IBaseThreadlinkGraphNodeCreationData
	{
		public Vector2 MousePosition { get; set; }
	}

	public abstract class GraphNode<NodeData> : Node
	where NodeData : IBaseThreadlinkGraphNodeCreationData
	{
		public string NodeTitle { get; set; }

		/// <summary>
		/// Finalize the creation of the node in the graph, after customizing it as necessary.
		/// </summary>
		/// <param name="graphPosition">The position of the node in the graph.</param>
		public virtual void Consolidate(NodeData nodeData)
		{
			NodeTitle = "New Node";

			var nodeTitleField = new TextField() { value = NodeTitle };

			nodeTitleField.SetFontSize(32);
			nodeTitleField.StretchToParentSize();

			SetPosition(new Rect(nodeData.MousePosition, Vector2.zero));

			titleContainer.Insert(0, nodeTitleField);
		}
	}

	public abstract class GraphView<NodeType, NodeData> : GraphView
	where NodeData : IBaseThreadlinkGraphNodeCreationData, new()
	where NodeType : GraphNode<NodeData>, new()
	{
		#region Editor Appearance and Functionality:
		public virtual void Build()
		{
			SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
			this.AddManipulator(new ContentDragger());
			this.AddManipulator(new SelectionDragger());
			this.AddManipulator(new RectangleSelector());

			var menuManipulators = CreateContextMenuManipulators();
			int count = menuManipulators.Count;
			for (int i = 0; i < count; i++) this.AddManipulator(menuManipulators[i]);

			var gridBG = new GridBackground();

			gridBG.StretchToParentSize();
			Insert(0, gridBG);
		}

		public virtual List<IManipulator> CreateContextMenuManipulators()
		{
			var result = new List<IManipulator>(1)
			{
				new ContextualMenuManipulator(menuEvent => menuEvent.menu.AppendAction("Add Node",
				actionEvent => AddElement(CreateNode(new NodeData
				{
					MousePosition = viewTransform.matrix.inverse.MultiplyPoint(actionEvent.eventInfo.localMousePosition)
				}))))
			};

			return result;
		}

		public virtual void Stylize(StyleSheet gridSheet)
		{
			if (gridSheet != null) styleSheets.Add(gridSheet);
		}
		#endregion

		#region Node Management:
		public virtual NodeType CreateNode(NodeData creationData)
		{
			var newNode = new NodeType();

			newNode.Consolidate(creationData);

			return newNode;
		}
		#endregion
	}

	public abstract class GraphViewWindow<View, Node, NodeData> : EditorWindow
	where NodeData : IBaseThreadlinkGraphNodeCreationData, new()
	where Node : GraphNode<NodeData>, new()
	where View : GraphView<Node, NodeData>, new()
	{
		[SerializeField] protected StyleSheet variablesSheet = null;
		[SerializeField] protected StyleSheet gridSheet = null;

		#region Paste this in your derived window to display it in the Editor. Customize the name as you see fit.
		/*[MenuItem("Threadlink/Your Window Name")]
		private static void DisplayWindow()
		{
			GetWindow<YourWindowType>("Your Window Name");
		}*/
		#endregion

		private void OnEnable() { Build(); }

		protected virtual void Build()
		{
			//Add the Graph View:
			var graphView = new View();

			graphView.Build();
			graphView.Stylize(gridSheet);
			graphView.StretchToParentSize();

			rootVisualElement.Add(graphView);

			//Stylize:
			if (variablesSheet != null) rootVisualElement.styleSheets.Add(variablesSheet);
		}
	}
}
