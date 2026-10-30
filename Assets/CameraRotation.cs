using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    [Header("Cel kamery")]
    [Tooltip("Obiekt, wokół którego kamera będzie się obracać (np. Płyta)")]
    public Transform target;

    [Header("Ustawienia sterowania")]
    public float distance = 10f;             // Odległość od obiektu
    public float sensitivity = 3f;           // Czułość obrotu myszką
    public float scrollSensitivity = 5f;     // Czułość kółka myszy (przybliżanie)

    [Header("Ograniczenia kąta (Pion)")]
    public float minPitch = 5f;              // Kamera nie zejdzie poniżej tego kąta (nie wejdzie pod płytę)
    public float maxPitch = 85f;             // Kamera nie przejdzie przez zenit

    private float yaw = 0f;
    private float pitch = 30f;

    void Start()
    {
        if (target != null)
        {
            // Dopasuj początkowe kąty do aktualnego widoku
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Obrót zachodzi tylko, gdy trzymasz Prawy Przycisk Myszy (1)
        // Dzięki temu Lewy Przycisk (0) jest wolny do obsługi UI (suwaki, przyciski)
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;

            // Zablokowanie obrotu góra/dół, żeby nie "wywrócić" kamery
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Obsługa kółka myszy (Scroll) do przybliżania i oddalania
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * scrollSensitivity;
        distance = Mathf.Clamp(distance, 2f, 30f); // Minimalna i maksymalna odległość

        // Zastosowanie nowych wartości do kamery
        Quaternion currentRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 newPosition = target.position - (currentRotation * Vector3.forward * distance);

        transform.position = newPosition;
        transform.LookAt(target);
    }
}