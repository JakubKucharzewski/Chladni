using UnityEngine;

public class GizmoCircle : MonoBehaviour
{
    public int xSize, zSize;

    private Vector3[] vertices;

    private Mesh mesh;

    private void Awake()
    {
        Generate();
    }

    private void Generate()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Grid";

        vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        for (int i = 0, z = 0; z <= zSize; z++)
            for (int x = 0; x <= xSize; x++, i++)
                vertices[i] = new Vector3(x, 0, z);

        mesh.vertices = vertices;

        int[] triangles = new int[xSize * zSize * 6];
        for (int ti = 0, vi = 0, y = 0; y < zSize; y++, vi++)
            for (int x = 0; x < xSize; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
                triangles[ti + 5] = vi + xSize + 2;
            }
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}