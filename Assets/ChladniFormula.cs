using MathNet.Numerics;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ChladniFormula : MonoBehaviour
{
    Mesh mesh;
    public Gradient gradient;
    Vector3[] vertices;
    Color[] colors;
    float A = 0.2f;
    float B = 0.2f;
    float C = 0.2f;
    float D = 0.2f;
    float a = 1f;

    int n = 1;
    int m = 4;


    void Start()
    {

        mesh = GetComponent<MeshFilter>().mesh;
        

        vertices = mesh.vertices; //Tablica wierzchołków
        colors = new Color[vertices.Length]; // tablica kolorów (nawias [] oznacza ze tworzy tablice, w przypadku () utworzył pojedynczy kolor

    }

    void Update()
    {
        Vector3[] currentVertices = mesh.vertices;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        float R = renderer.bounds.extents.x;

        for (var i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            float u = 0f;

            float radius = math.sqrt(vertex.x * vertex.x + vertex.z * vertex.z);
            float theta = math.atan2(vertex.z, vertex.x);
            

            if (radius < R*1.1)
            {
                float eigenVal = a * BesselRoots[n, m] / R;
                float timeVal = eigenVal * Time.time*0.1f;

                float u1 = V_nm1(radius, theta, n, m, R) * (A * math.cos(timeVal) + B * math.sin(timeVal));
                float u2 = V_nm2(radius, theta, n, m, R) * (C * math.cos(timeVal) + D * math.sin(timeVal));

                u += u1 + u2;
                float coef = R * 0.1f;

                vertex.y = u;

                float lerpValue = (u + 0.2f) / 0.4f;
                colors[i] = gradient.Evaluate(lerpValue);
                
                //;
                //colors[i] = Color.Lerp(Color.black, Color.white, lerpValue);


                

                currentVertices[i] = vertex;
                    

            }
            ;


        }

        //Vector3 vert = vertices[10];
        //Debug.Log(vert.y);
        // assign the local vertices array into the vertices array of the Mesh.
        mesh.vertices = currentVertices;
        mesh.colors = colors;
        mesh.RecalculateNormals(); // Wymagane, by cienie na falach liczyły się poprawnie
        mesh.RecalculateBounds();
    }

    public static float[,] BesselRoots = new float[,]
    {
            { 2.4048f, 5.5201f, 8.6537f, 11.7915f, 14.9309f },
            { 3.8317f, 7.0156f, 10.1735f, 13.3237f, 16.4706f },
            { 5.1356f, 8.4172f, 11.6198f, 14.7960f, 17.9598f },
            { 6.3802f, 9.7610f, 13.0152f, 16.2235f, 19.4094f},
            { 7.5883f, 11.0647f, 14.3725f, 17.6160f, 20.8269f }
    };

    public static float V_nm1(float radius, float theta, int n, int m, float R)
    {
        float root = BesselRoots[n, m];

        float bessel = (float)SpecialFunctions.BesselJ(n, root * radius / R);

        return bessel * math.cos(n * theta);
    }

    public static float V_nm2(float radius, float theta, int n, int m, float R)
    {
        float root = BesselRoots[n, m];

        float bessel = (float)SpecialFunctions.BesselJ(n, root * radius / R);
        return bessel * math.sin(n * theta);
    }

}

