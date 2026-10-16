using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    public sealed class CoinView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer coin;
        [SerializeField] float bobHeight = 0.05f;

        float phase;

        public void Begin(int id) => phase = (id * 0.37f) % 1f;

        public void Sync(World w, CoinState c, float alpha)
        {
            Vector2 pos = Vector2.Lerp(c.PrevPosition, c.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);
            coin.transform.localPosition = new Vector3(0, 0.12f + Mathf.Sin((Time.time + phase) * 6f) * bobHeight, 0);
            coin.enabled = !CoinSystem.IsBlinkingOff(w, c, Time.time);
        }
    }
}
