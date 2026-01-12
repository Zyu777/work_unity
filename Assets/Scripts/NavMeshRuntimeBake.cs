using UnityEngine;
using Unity.AI.Navigation;

public class NavMeshRuntimeBake : MonoBehaviour
{
    public NavMeshSurface surface;

    void Awake()
    {
        if (surface == null)
            surface = GetComponent<NavMeshSurface>();

        if (surface != null)
        {
            surface.BuildNavMesh();
        }
    }
}