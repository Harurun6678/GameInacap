using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxBackground : MonoBehaviour
{
    [Header("Camara")]
    [Tooltip("Si esta vacio, usa la Main Camera.")]
    [SerializeField] private Camera targetCamera;

    [Header("Factor de Parallax")]
    [Tooltip("0 = fijo en el mundo. 1 = acompana a la camara y parece muy lejano.")]
    [SerializeField, Range(0f, 1f)] private float parallaxX = 0.3f;
    [SerializeField, Range(0f, 1f)] private float parallaxY = 0.1f;

    [Header("Repeticion Horizontal")]
    [Tooltip("Repite la capa a izquierda y derecha para que el nivel no tenga fin.")]
    [SerializeField] private bool loopHorizontal = true;
    [Tooltip("Ancho de una copia en unidades del mundo. Si es 0, se calcula desde el sprite.")]
    [SerializeField] private float tileWidth = 0f;

    private SpriteRenderer spriteRenderer;
    private Vector3 startPos;
    private Vector3 camStart;
    private float layerOriginX;

    public void Configure(Camera camera, float horizontal, float vertical)
    {
        targetCamera = camera;
        parallaxX = Mathf.Clamp01(horizontal);
        parallaxY = Mathf.Clamp01(vertical);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("ParallaxBackground: no hay camara asignada ni Main Camera.", this);
            enabled = false;
            return;
        }

        startPos = transform.position;
        camStart = targetCamera.transform.position;
        layerOriginX = startPos.x;

        if (loopHorizontal) SetupTiles();
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 camDelta = targetCamera.transform.position - camStart;

        if (loopHorizontal)
        {
            // Posicion en el mundo que la capa necesita cubrir alrededor de la camara.
            float coverX = camStart.x + camDelta.x * (1f - parallaxX);

            // Reubica el origen de la capa en pasos de tileWidth para que siempre haya copia visible.
            layerOriginX += Mathf.Round((coverX - layerOriginX) / tileWidth) * tileWidth;
        }

        float x = layerOriginX + camDelta.x * parallaxX;
        float y = startPos.y + camDelta.y * parallaxY;

        transform.position = new Vector3(x, y, startPos.z);
    }

    private void SetupTiles()
    {
        if (spriteRenderer.sprite == null)
        {
            Debug.LogWarning("ParallaxBackground: el SpriteRenderer no tiene sprite. Se desactiva la repeticion.", this);
            loopHorizontal = false;
            return;
        }

        if (tileWidth <= 0f) tileWidth = spriteRenderer.bounds.size.x;

        if (tileWidth <= Mathf.Epsilon || Mathf.Abs(transform.lossyScale.x) <= Mathf.Epsilon)
        {
            loopHorizontal = false;
            return;
        }

        // Convierte el ancho del mundo a espacio local, por si el objeto tiene escala.
        float localOffset = tileWidth / transform.lossyScale.x;

        CreateCopy(-localOffset);
        CreateCopy(localOffset);
    }

    private void CreateCopy(float localX)
    {
        GameObject go = new GameObject(name + "_Copy");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(localX, 0f, 0f);

        SpriteRenderer copy = go.AddComponent<SpriteRenderer>();
        copy.sprite = spriteRenderer.sprite;
        copy.color = spriteRenderer.color;
        copy.sharedMaterial = spriteRenderer.sharedMaterial;
        copy.sortingLayerID = spriteRenderer.sortingLayerID;
        copy.sortingOrder = spriteRenderer.sortingOrder;
        copy.flipX = spriteRenderer.flipX;
        copy.flipY = spriteRenderer.flipY;
    }
}
