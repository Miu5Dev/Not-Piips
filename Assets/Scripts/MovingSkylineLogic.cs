using UnityEngine;

public class MovingSkylineLogic : MonoBehaviour
{
    [SerializeField] private Renderer rend;
    [SerializeField] private float speed = 0.05f;

    private Vector2 offset;

    void Update()
    {
        offset.x -= speed * Time.deltaTime;
        rend.material.SetTextureOffset("_BaseMap", offset);
    }
}