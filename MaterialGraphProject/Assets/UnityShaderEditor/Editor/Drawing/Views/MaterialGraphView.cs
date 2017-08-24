using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Graphing;
using UnityEngine.MaterialGraph;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements;
using Edge = UnityEditor.Experimental.UIElements.GraphView.Edge;
using MouseButton = UnityEngine.Experimental.UIElements.MouseButton;

namespace UnityEditor.MaterialGraph.Drawing
{
    public sealed class MaterialGraphView : GraphView
    {
        [SerializeField]
        private GraphDrawingData m_DrawingData = new GraphDrawingData();

        public GraphDrawingData drawingData
        {
            get { return m_DrawingData; }
        }

        public MaterialGraphView(EditorWindow editorWindow)
        {
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ClickSelector());

            Insert(0, new GridBackground());

            RegisterCallback<MouseUpEvent>(DoContextMenu);

            typeFactory[typeof(MaterialNodePresenter)] = typeof(MaterialNodeView);
            typeFactory[typeof(GraphAnchorPresenter)] = typeof(NodeAnchor);
            typeFactory[typeof(EdgePresenter)] = typeof(Edge);

            AddStyleSheetPath("Styles/MaterialGraph");
        }

        public bool CanAddToNodeMenu(Type type)
        {
            return true;
        }

        void DoContextMenu(MouseUpEvent evt)
        {
            if (evt.button == (int)MouseButton.RightMouse)
            {
                var gm = new GenericMenu();
                foreach (Type type in Assembly.GetAssembly(typeof(AbstractMaterialNode)).GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract && (type.IsSubclassOf(typeof(AbstractMaterialNode))))
                    {
                        var attrs = type.GetCustomAttributes(typeof(TitleAttribute), false) as TitleAttribute[];
                        if (attrs != null && attrs.Length > 0 && CanAddToNodeMenu(type))
                        {
                            gm.AddItem(new GUIContent(attrs[0].m_Title), false, AddNode, new AddNodeCreationObject(type, evt.mousePosition));
                        }
                    }
                }

                gm.ShowAsContext();
            }
            evt.StopPropagation();
        }

        class AddNodeCreationObject
        {
            public Vector2 m_Pos;
            public readonly Type m_Type;

            public AddNodeCreationObject(Type t, Vector2 p)
            {
                m_Type = t;
                m_Pos = p;
            }
        };

        void AddNode(object obj)
        {
            var posObj = obj as AddNodeCreationObject;
            if (posObj == null)
                return;

            INode node;
            try
            {
                node = Activator.CreateInstance(posObj.m_Type) as INode;
            }
            catch (Exception e)
            {
                Debug.LogWarningFormat("Could not construct instance of: {0} - {1}", posObj.m_Type, e);
                return;
            }

            if (node == null)
                return;
            var drawstate = node.drawState;

            Vector3 localPos = contentViewContainer.transform.matrix.inverse.MultiplyPoint3x4(posObj.m_Pos);
            drawstate.position = new Rect(localPos.x, localPos.y, 0, 0);
            node.drawState = drawstate;

            var graphDataSource = GetPresenter<MaterialGraphPresenter>();
            graphDataSource.AddNode(node);
        }

        public override void OnDataChanged()
        {
            base.OnDataChanged();

            var graphDataSource = GetPresenter<MaterialGraphPresenter>();
            if (graphDataSource == null)
                return;

            if (drawingData.selection.SequenceEqual(selection.OfType<MaterialNodeView>().Select(d => ((MaterialNodePresenter)d.presenter).node.guid))) return;

            var selectedDrawers = drawingData.selection
                .Select(guid => contentViewContainer
                    .OfType<MaterialNodeView>()
                    .FirstOrDefault(drawer => ((MaterialNodePresenter)drawer.presenter).node.guid == guid))
                .ToList();

            ClearSelection();
            foreach (var drawer in selectedDrawers)
                AddToSelection(drawer);
        }

        void PropagateSelection()
        {
            var graphDataSource = GetPresenter<MaterialGraphPresenter>();
            if (graphDataSource == null)
                return;

            var selectedNodes = selection.OfType<MaterialNodeView>().Select(x => (MaterialNodePresenter)x.presenter);
            graphDataSource.UpdateSelection(selectedNodes);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            PropagateSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            PropagateSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            PropagateSelection();
        }
    }
}
