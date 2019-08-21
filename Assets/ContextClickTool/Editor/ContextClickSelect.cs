using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static public class ContextClickSelect
{
    const int MAX_OBJ_FOUND = 30;
    const string LEVEL_SEPARATOR = "          ";
    const string PREFAB_TAG = "♦";

    static ContextClickSelect()
    {
        if (EditorApplication.isPlaying) return;
        SceneView.onSceneGUIDelegate += OnSceneGUI;
        FindMethodByReflection();
    }
    
    static bool clickDown = false;
    static Vector2 clickDownPos;

    static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            clickDownPos = e.mousePosition;
            clickDown = true;
        }
        else if (e.type == EventType.MouseUp && e.button == 1 && clickDown)
        {
            clickDown = false;
            if (clickDownPos == e.mousePosition)
            {
                e.Use();
                OpenContextMenu(e.mousePosition, sceneView);
            }
        }
    }

    static void OpenContextMenu(Vector2 pos, SceneView sceneView)
    {
        var invertedPos = new Vector2(pos.x, sceneView.position.height - 16 - pos.y);

        GenericMenu contextMenu = new GenericMenu();
        GameObject obj = null;
        int matIndex;
        
        Dictionary<Transform, List<Transform>> parentChildsDict = new Dictionary<Transform, List<Transform>>();
        GameObject[] currArray = null;

        for (int i = 0; i <= MAX_OBJ_FOUND; i++)
        {
            if (parentChildsDict.Count > 0)
            {
                currArray = new GameObject[parentChildsDict.Count];
                int arrayIndex = 0;
                foreach (var parent in parentChildsDict)
                {
                    currArray[arrayIndex] = parent.Key.gameObject;
                    arrayIndex++;
                }
            }

            obj = PickObjectOnPos(sceneView.camera, ~0, invertedPos, currArray, null, out matIndex);
            if (obj != null)
            {
                parentChildsDict[obj.transform] = new List<Transform>();

                var currentParent = obj.transform.parent;
                var lastParent = obj.transform;
                List<Transform> currentChilds;
                while (currentParent != null)
                {
                    if (parentChildsDict.TryGetValue(currentParent, out currentChilds))
                        currentChilds.Add(lastParent);
                    else
                        parentChildsDict.Add(currentParent, new List<Transform>() { lastParent });

                    lastParent = currentParent;
                    currentParent = currentParent.parent;
                }
            }
            else
                break;
        }

        foreach (var parentChild in parentChildsDict.Where(keyValue => keyValue.Key.parent == null))
            CreateMenuRecu(contextMenu, parentChild.Key, "", parentChildsDict);

        if (parentChildsDict.Count == 0)
            AddMenuItem(contextMenu, "None", null);

        contextMenu.DropDown(new Rect(pos, Vector2.zero));
    }
    
    static void CreateMenuRecu(GenericMenu menu, Transform current, string currentPath, Dictionary<Transform, List<Transform>> parentChilds)
    {
        AddMenuItem(menu, currentPath + (IsPrefab(current.gameObject) ? PREFAB_TAG : "") + current.name, current);
        List<Transform> childs;
        if (!parentChilds.TryGetValue(current, out childs)) return;
        if (childs == null) return;
        foreach (var child in childs)
            CreateMenuRecu(menu, child, currentPath + LEVEL_SEPARATOR, parentChilds);
    }
    

    static bool IsPrefab(GameObject obj)
    {
        var prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
        return prefab != null && prefab == obj;
    }

    static GameObject PickObjectOnPos(Camera cam, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex) // PICK A GAMEOBJECT FROM SCENE VIEW AT POSITION
    {
        materialIndex = -1;
        return (GameObject)Internal_PickClosestGO.Invoke(null, new object[] { cam, layers, position, ignore, filter, materialIndex });
    }

    //CONTEXT MENU

    static void AddMenuItem(GenericMenu menu, string menuPath, Transform asset) //ADD ITEM TO MENU
    {
        menu.AddItem(new GUIContent(menuPath), false, OnItemSelected, asset);
    }

    private static void OnItemSelected(object itemSelected) // ON CLICK ITEM ON LIST
    {
        if (itemSelected != null)
            Selection.activeTransform = itemSelected as Transform;
    }

    //REFLECTION
    static private MethodInfo Internal_PickClosestGO;

    static void FindMethodByReflection()
    {
        Assembly editorAssembly = typeof(Editor).Assembly;
        System.Type handleUtilityType = editorAssembly.GetType("UnityEditor.HandleUtility");
        Internal_PickClosestGO = handleUtilityType.GetMethod("Internal_PickClosestGO", BindingFlags.Static | BindingFlags.NonPublic);
    }
}
