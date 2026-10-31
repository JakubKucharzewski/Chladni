using System.Collections.Generic;
using MathNet.Numerics;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class ChladniFormula : MonoBehaviour
{
    private Mesh mesh;
    public Gradient gradient;

    [Header("Wave Propagation Speed")]
    public float a = 1f;

    [Header("Active Vibration Modes (Physics)")]
    public List<Vector2Int> activeModes = new List<Vector2Int>();

    [Header("Demonstration Mode")]
    public bool forceMaxAmplitudes = true;

    [Header("UI References (Dynamic List)")]
    public GameObject modeItemPrefab;    
    public Transform modeListContainer;  

    [Header("UI References (Slider Values)")]
    public TextMeshProUGUI pendingNText; 
    public TextMeshProUGUI pendingMText; 

    [Header("Projection Button")]
    public Button projectionButtonVisibility;
    public TextMeshProUGUI projectionText;

    [Header("View Button")]
    public TextMeshProUGUI ViewText;

    [Header("Cameras")]
    public Camera mainCamera;       
    public Camera topDownCamera;    

    [Header("Lighting (Position Switching)")]
    public Transform mainLight;       
    public Transform lightAnchor3D;   
    public Transform lightAnchor2D;   

    [Header("Interactive Hit (Raycast)")]
    public bool enableMouseHit = true;
    public float hitImpulse = 15f;           
    public float hitWidth = 35f;             

    private float currentHitRadius = 0f;
    private float currentHitTheta = 0f;
    private float hitTime = 0f;              

    [Header("UI Toggles")]
    public UnityEngine.UI.Toggle maxAmplitudesToggle;

    [Header("Vibration Damping")]
    [Range(0f, 5f)] public float damping = 2.5f; 

    [Header("Cursor (Hover Settings)")]
    public Texture2D hoverCursor;        
    public Texture2D clickCursor;        
    public Vector2 cursorHotspot = Vector2.zero;

    private bool isHovering = false;
    private bool isClicking = false;     

    private Dictionary<Vector2Int, GameObject> spawnedUIItems = new Dictionary<Vector2Int, GameObject>();

    private int pendingN = 0;
    private int pendingM = 1;

    private float[,] A_coeff = new float[5, 5];
    private float[,] B_coeff = new float[5, 5];
    private float[,] C_coeff = new float[5, 5];
    private float[,] D_coeff = new float[5, 5];

    private Vector3[] currentVertices;
    private Color[] colors;
    private float[] radii;
    private float[] thetas;
    private float R;
    private float maxRadius;
    private int vertexCount;
    private float[,,] besselCache;

    public static readonly float[,] BesselRoots = new float[,]
    {
        { 2.4048f, 5.5201f, 8.6537f, 11.7915f, 14.9309f },
        { 3.8317f, 7.0156f, 10.1735f, 13.3237f, 16.4706f },
        { 5.1356f, 8.4172f, 11.6198f, 14.7960f, 17.9598f },
        { 6.3802f, 9.7610f, 13.0152f, 16.2235f, 19.4094f },
        { 7.5883f, 11.0647f, 14.3725f, 17.6160f, 20.8269f }
    };

    void Start()
    {
        // Add a MeshCollider for raycasting if not present on the GameObject
        if (GetComponent<Collider>() == null)
        {
            MeshCollider meshCol = gameObject.AddComponent<MeshCollider>();
            meshCol.sharedMesh = GetComponent<MeshFilter>().mesh;
        }

        // Initialize mesh references and mark as dynamic for performance
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;
        mesh.MarkDynamic();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        R = meshRenderer.bounds.extents.x;
        maxRadius = R * 1.1f;

        Vector3[] baseVertices = mesh.vertices;
        vertexCount = baseVertices.Length;

        // Allocate memory for mesh data and caching
        currentVertices = new Vector3[vertexCount];
        colors = new Color[vertexCount];
        radii = new float[vertexCount];
        thetas = new float[vertexCount];
        besselCache = new float[vertexCount, 5, 5];

        // Cache polar coordinates and Bessel function results to optimize the Update loop
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 v = baseVertices[i];
            currentVertices[i] = v;
            
            float r = math.sqrt(v.x * v.x + v.z * v.z);
            radii[i] = r;
            thetas[i] = math.atan2(v.z, v.x);

            for (int n = 0; n < 5; n++)
            {
                for (int m = 0; m < 5; m++)
                {
                    besselCache[i, n, m] = (float)SpecialFunctions.BesselJ(n, BesselRoots[n, m] * r / R);
                }
            }
        }

        CalculateAllCoefficients();

        // Rebuild UI elements for any modes pre-configured in the Inspector
        foreach (var mode in new List<Vector2Int>(activeModes))
        {
            activeModes.Remove(mode);
            AddMode(mode.x, mode.y);
        }

        if (pendingNText != null) pendingNText.text = "" + pendingN;
        if (pendingMText != null) pendingMText.text = "" + pendingM;
    }

    void Update()
    {
        // Handle custom cursor graphics on mouse hover/click
        CheckCursorHover();

        // Process mouse input for physical interaction with the mesh
        if (enableMouseHit && UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseHit();
        }

        float[,] timeFactor1 = new float[5, 5];
        float[,] timeFactor2 = new float[5, 5];
        float maxExpectedAmplitude = 0f;

        // Calculate time delta for wave propagation and damping factor
        float realElapsedTime = Time.time - hitTime;
        float elapsedTime = realElapsedTime * 0.1f; 
        float dampingFactor = math.exp(-damping * realElapsedTime);

        // Precalculate time factors for all active modes
        foreach (Vector2Int mode in activeModes)
        {
            int n = math.clamp(mode.x, 0, 4);
            int m_idx = math.clamp(mode.y - 1, 0, 4);

            float root = BesselRoots[n, m_idx];
            float omega = (a * root) / R;
            float timeVal = omega * elapsedTime;

            float cosT = math.cos(timeVal);
            float sinT = math.sin(timeVal);

            float ampA = A_coeff[n, m_idx];
            float ampB = B_coeff[n, m_idx];
            float ampC = C_coeff[n, m_idx];
            float ampD = D_coeff[n, m_idx];

            // Normalize mode amplitudes in demonstration mode while preserving rotation
            if (forceMaxAmplitudes)
            {
                float magnitude = math.sqrt(ampA * ampA + ampB * ampB + ampC * ampC + ampD * ampD);

                if (magnitude > 0.00001f)
                {
                    ampA /= magnitude;
                    ampB /= magnitude;
                    ampC /= magnitude;
                    ampD /= magnitude;
                }
                else
                {
                    // Fallback excitation if the node is struck directly
                    ampB = 1f;
                    ampD = (n > 0) ? 1f : 0f;
                }
            }

            // Apply calculated amplitudes and damping
            timeFactor1[n, m_idx] = (ampA * cosT + ampB * sinT) * dampingFactor;
            timeFactor2[n, m_idx] = (ampC * cosT + ampD * sinT) * dampingFactor;

            maxExpectedAmplitude += math.abs(ampA) + math.abs(ampB) + math.abs(ampC) + math.abs(ampD);
        }

        if (maxExpectedAmplitude < 0.001f) maxExpectedAmplitude = 1f;

        // Apply deformation and color gradients to all mesh vertices
        for (int i = 0; i < vertexCount; i++)
        {
            if (radii[i] < maxRadius)
            {
                float theta = thetas[i];
                float u_total = 0f;

                foreach (Vector2Int mode in activeModes)
                {
                    int n = math.clamp(mode.x, 0, 4);
                    int m_idx = math.clamp(mode.y - 1, 0, 4);

                    float cos_nth = math.cos(n * theta);
                    float sin_nth = math.sin(n * theta);
                    float bessel = besselCache[i, n, m_idx];

                    u_total += bessel * (cos_nth * timeFactor1[n, m_idx] + sin_nth * timeFactor2[n, m_idx]);
                }

                currentVertices[i].y = u_total;
                
                float normalizedU = (u_total + maxExpectedAmplitude) / (2f * maxExpectedAmplitude);
                colors[i] = gradient.Evaluate(math.clamp(normalizedU, 0f, 1f));
            }
        }
        
        // Update the visual representation of the mesh
        mesh.vertices = currentVertices;
        mesh.colors = colors;
        mesh.RecalculateNormals();
    }

    // ------------------------------------------
    // DYNAMIC UI EVENT METHODS
    // ------------------------------------------

    public void SetPendingN(float value)
    {
        pendingN = Mathf.RoundToInt(value);
        if (pendingNText != null)
        {
            pendingNText.text = "" + pendingN;
        }
    }

    public void SetPendingM(float value)
    {
        pendingM = Mathf.RoundToInt(value);
        if (pendingMText != null)
        {
            pendingMText.text = "" + pendingM;
        }
    }

    public void SetSpeed(float value)
    {
        a = Mathf.RoundToInt(value);
    }

    public void AddPendingMode()
    {
        AddMode(pendingN, pendingM);
    }

    private void AddMode(int n, int m)
    {
        Vector2Int newMode = new Vector2Int(n, m);

        // Prevent duplicate modes in the list
        if (!activeModes.Contains(newMode))
        {
            activeModes.Add(newMode);

            // Instantiate UI prefab for the new mode
            GameObject newItem = Instantiate(modeItemPrefab, modeListContainer);

            Text itemText = newItem.GetComponentInChildren<Text>();
            if (itemText != null)
            {
                itemText.text = $"   n: {newMode.x}, m: {newMode.y}";
            }

            // Assign removal method to the instantiated button
            Button deleteButton = newItem.GetComponentInChildren<Button>();
            if (deleteButton != null)
            {
                Vector2Int modeToRemove = newMode;
                deleteButton.onClick.AddListener(() => RemoveSpecificMode(modeToRemove));
            }

            spawnedUIItems.Add(newMode, newItem);
        }
    }

    public void RemoveSpecificMode(Vector2Int modeToRemove)
    {
        if (activeModes.Contains(modeToRemove))
        {
            activeModes.Remove(modeToRemove);
        }

        if (spawnedUIItems.ContainsKey(modeToRemove))
        {
            Destroy(spawnedUIItems[modeToRemove]);
            spawnedUIItems.Remove(modeToRemove);
        }
    }

    public void ClearAllModes()
    {
        activeModes.Clear();
        foreach (var kvp in spawnedUIItems)
        {
            Destroy(kvp.Value); 
        }
        spawnedUIItems.Clear();
    }

    public void ChangeMode()
    {
        ClearAllModes();
        AddMode(pendingN, pendingM);
    }

    // ------------------------------------------
    // CALCULATIONS & PHYSICS
    // ------------------------------------------

    private void CalculateAllCoefficients()
    {
        // Defines the resolution of the integration grid
        int rSteps = 60;
        int thetaSteps = 60;
        float dr = R / rSteps;
        float dtheta = 2f * Mathf.PI / thetaSteps;

        System.Array.Clear(A_coeff, 0, A_coeff.Length);
        System.Array.Clear(B_coeff, 0, B_coeff.Length);
        System.Array.Clear(C_coeff, 0, C_coeff.Length);
        System.Array.Clear(D_coeff, 0, D_coeff.Length);

        // Perform numerical integration over the membrane surface
        for (int i = 0; i < rSteps; i++)
        {
            float r = (i + 0.5f) * dr;
            for (int j = 0; j < thetaSteps; j++)
            {
                float theta = -Mathf.PI + (j + 0.5f) * dtheta;
                float f1 = InitialDisplacement_f1(r, theta);
                float f2 = InitialVelocity_f2(r, theta);
                float dArea = r * dr * dtheta;

                for (int n = 0; n < 5; n++)
                {
                    float cos_nth = math.cos(n * theta);
                    float sin_nth = math.sin(n * theta);

                    for (int m = 0; m < 5; m++)
                    {
                        float root = BesselRoots[n, m];
                        float bessel_r = (float)SpecialFunctions.BesselJ(n, root * r / R);

                        A_coeff[n, m] += f1 * bessel_r * cos_nth * dArea;
                        B_coeff[n, m] += f2 * bessel_r * cos_nth * dArea;

                        if (n > 0)
                        {
                            C_coeff[n, m] += f1 * bessel_r * sin_nth * dArea;
                            D_coeff[n, m] += f2 * bessel_r * sin_nth * dArea;
                        }
                    }
                }
            }
        }

        // Normalize computed coefficients based on orthogonal properties
        for (int n = 0; n < 5; n++)
        {
            float epsilon = (n == 0) ? 2f : 1f;
            for (int m = 0; m < 5; m++)
            {
                float root = BesselRoots[n, m];
                float omega = (a * root) / R;
                float J_np1 = (float)SpecialFunctions.BesselJ(n + 1, root);
                float normFactor = 2f / (Mathf.PI * R * R * J_np1 * J_np1);

                A_coeff[n, m] *= (normFactor / epsilon);
                B_coeff[n, m] *= (normFactor / (epsilon * omega));

                if (n > 0)
                {
                    C_coeff[n, m] *= normFactor;
                    D_coeff[n, m] *= (normFactor / omega);
                }
            }
        }
    }

    private float InitialDisplacement_f1(float r, float theta)
    {
        return 0f;
    }

    private float InitialVelocity_f2(float r, float theta)
    {
        // Distance from the exact point of impact via the law of cosines
        float distanceSq = r * r + currentHitRadius * currentHitRadius
                         - 2f * r * currentHitRadius * math.cos(theta - currentHitTheta);

        // Calculate Gaussian distribution defining the impulse area
        return hitImpulse * math.exp(-hitWidth * distanceSq / (R * R));
    }

    // ------------------------------------------
    // CAMERA & LIGHT MANAGEMENT
    // ------------------------------------------

    public void ToggleCamera()
    {
        if (mainCamera != null && topDownCamera != null)
        {
            bool isMainActive = mainCamera.gameObject.activeSelf;
            
            mainCamera.gameObject.SetActive(!isMainActive);
            topDownCamera.gameObject.SetActive(isMainActive);

            // Synchronize directional light angle with the active camera perspective
            if (mainLight != null)
            {
                Transform targetAnchor = isMainActive ? lightAnchor2D : lightAnchor3D;

                if (targetAnchor != null)
                {
                    mainLight.position = targetAnchor.position;
                    mainLight.rotation = targetAnchor.rotation; 
                }
            }
        }
    }

    public void ToggleViewText()
    {
        if (ViewText != null)
        {
            ViewText.text = (ViewText.text == "3D") ? "2D" : "3D";
        }
    }

    public void ToggleViewMaxEqualAmplitudes()
    {
        // Enforce specific logic states for different camera views
        if (topDownCamera.gameObject.activeSelf)
        {
            forceMaxAmplitudes = false;
            projectionButtonVisibility.gameObject.SetActive(false);
            maxAmplitudesToggle.gameObject.SetActive(false);
            
            if (maxAmplitudesToggle != null)
            {
                maxAmplitudesToggle.SetIsOnWithoutNotify(false);
            }
        }
        else
        {
            forceMaxAmplitudes = true;
            projectionButtonVisibility.gameObject.SetActive(true);
            maxAmplitudesToggle.gameObject.SetActive(true);
            
            if (maxAmplitudesToggle != null)
            {
                maxAmplitudesToggle.SetIsOnWithoutNotify(true);
            }
        }
    }

    public void ToggleForceMaxAmplitudes(bool isOn)
    {
        forceMaxAmplitudes = isOn;
    }

    public void ToggleProjection()
    {
        if (mainCamera != null)
        {
            mainCamera.orthographic = !mainCamera.orthographic;
        }
    }

    public void ToggleProjectionText()
    {
        if (projectionText != null) 
        {
            projectionText.text = (projectionText.text == "Orthographic") ? "Perspective" : "Orthographic";
        }
    }

    // ------------------------------------------
    // INPUT INTERACTIONS
    // ------------------------------------------

    private void HandleMouseHit()
    {
        // Prevent triggering mesh hits when interacting with UI elements
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera activeCam = (mainCamera != null && mainCamera.gameObject.activeSelf) ? mainCamera : topDownCamera;
        if (activeCam == null) activeCam = Camera.main;
        if (activeCam == null) return;

        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = activeCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == this.transform)
            {
                // Translate world space intersection point into local mesh coordinates
                Vector3 localPoint = transform.InverseTransformPoint(hit.point);
                float hitR = math.sqrt(localPoint.x * localPoint.x + localPoint.z * localPoint.z);

                if (hitR <= R)
                {
                    currentHitRadius = hitR;
                    currentHitTheta = math.atan2(localPoint.z, localPoint.x);
                    hitTime = Time.time; 

                    CalculateAllCoefficients();
                }
            }
        }
    }

    public void SetDamping(float value)
    {
        damping = value;
    }

    private void CheckCursorHover()
    {
        if (hoverCursor == null) return;

        // Reset cursor back to default if hovering over UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            SetDefaultCursor();
            return;
        }

        Camera activeCam = (mainCamera != null && mainCamera.gameObject.activeSelf) ? mainCamera : topDownCamera;
        if (activeCam == null) activeCam = Camera.main;
        if (activeCam == null || UnityEngine.InputSystem.Mouse.current == null) return;

        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = activeCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == this.transform)
        {
            Vector3 localPoint = transform.InverseTransformPoint(hit.point);
            float hitR = math.sqrt(localPoint.x * localPoint.x + localPoint.z * localPoint.z);

            if (hitR <= R)
            {
                bool isPressed = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
                SetHoverCursor(isPressed);
            }
            else
            {
                SetDefaultCursor();
            }
        }
        else
        {
            SetDefaultCursor();
        }
    }

    private void SetHoverCursor(bool isPressed)
    {
        Texture2D targetCursor = (isPressed && clickCursor != null) ? clickCursor : hoverCursor;

        if (!isHovering || isClicking != isPressed)
        {
            Cursor.SetCursor(targetCursor, cursorHotspot, CursorMode.Auto);
            isHovering = true;
            isClicking = isPressed;
        }
    }

    private void SetDefaultCursor()
    {
        if (isHovering)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            isHovering = false;
            isClicking = false;
        }
    }
}