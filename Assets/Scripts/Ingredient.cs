using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Летящий ингредиент: баллистика, вращение и разрез на две половинки.</summary>
    public sealed class Ingredient : MonoBehaviour
    {
        public IngredientDef Def { get; private set; }
        public bool IsSliced { get; private set; }
        public Vector2 Position => transform.position;
        public float HitRadius => _radius * 1.1f;

        GameManager _game;
        Transform _body;
        Transform _shadow;
        SpriteRenderer _bodyRenderer;
        Vector2 _velocity;
        float _gravity;
        float _speed;
        float _spin;
        float _radius;

        public void Init(GameManager game, IngredientDef def, Vector2 position, Vector2 velocity,
            float gravity, float speed, float radius, int sortingOrder)
        {
            _game = game;
            Def = def;
            _velocity = velocity;
            _gravity = gravity;
            _speed = speed;
            _radius = radius;
            _spin = Random.Range(-200f, 200f);
            transform.position = position;

            // Тело крутится, подпись остаётся ровной — поэтому они на разных объектах.
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _bodyRenderer = _body.gameObject.AddComponent<SpriteRenderer>();
            _bodyRenderer.sortingOrder = sortingOrder + 1;

            var sprite = Art.IngredientSprite(def);
            if (sprite != null)
            {
                _bodyRenderer.sprite = sprite;
                // У эмодзи есть прозрачные поля, поэтому картинка чуть крупнее радиуса попадания.
                var size = sprite.bounds.size;
                _body.localScale = Vector3.one * (2.25f * radius / Mathf.Max(size.x, size.y));

                _shadow = NewSprite("Shadow", transform, sprite, new Color(0f, 0f, 0f, 0.3f), sortingOrder).transform;
                _shadow.localPosition = new Vector3(0.07f, -0.1f, 0f);
                _shadow.localScale = _body.localScale;
            }
            else
            {
                BuildPlaceholder(def, radius, sortingOrder);
            }
        }

        void BuildPlaceholder(IngredientDef def, float radius, int sortingOrder)
        {
            _bodyRenderer.sprite = Art.Circle;
            _bodyRenderer.color = def.Color;
            _body.localScale = Vector3.one * (2f * radius);

            var rim = NewSprite("Rim", _body, Art.Circle, def.Color * 0.6f + new Color(0f, 0f, 0f, 0.4f), sortingOrder);
            rim.transform.localScale = Vector3.one * 1.08f;

            // Блик не крутится вместе с телом: свет всегда сверху слева и не наезжает на подпись.
            var shine = NewSprite("Shine", transform, Art.Circle, new Color(1f, 1f, 1f, 0.35f), sortingOrder + 2);
            shine.transform.localPosition = new Vector3(-0.3f * radius / 0.72f, 0.36f * radius / 0.72f, 0f);
            shine.transform.localScale = Vector3.one * (0.36f * radius);

            NewLabel("LabelShadow", def.Name, new Color(0f, 0f, 0f, 0.7f), new Vector3(0.025f, -0.025f, 0f), sortingOrder + 3);
            NewLabel("Label", def.Name, Color.white, Vector3.zero, sortingOrder + 4);
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void NewLabel(string name, string text, Color color, Vector3 offset, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.UIFont;
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Art.UIFont.material;
            mr.sortingOrder = order;
        }

        void Update()
        {
            float dt = Time.deltaTime * _speed;
            _velocity.y -= _gravity * dt;
            transform.position += (Vector3)(_velocity * dt);
            _body.Rotate(0f, 0f, _spin * dt);
            if (_shadow != null) _shadow.localRotation = _body.localRotation;

            if (_velocity.y < 0f && transform.position.y < -_game.ViewHalfHeight - _radius * 1.5f)
                _game.OnIngredientLost(this);
        }

        /// <summary>
        /// Разрезать вдоль направления свайпа. Объект уничтожается, вместо него половинки,
        /// которые летят в ёмкость блюда (если она есть).
        /// </summary>
        public void Slice(Vector2 direction, Container container, SliceResult result)
        {
            IsSliced = true;

            // Режем в локальных осях тела, чтобы картинка сохранила текущий поворот.
            Vector2 localDirection = Quaternion.Inverse(_body.rotation) * direction;
            var normal = new Vector2(-direction.y, direction.x);
            var source = _bodyRenderer.sprite;
            SpawnHalf(Art.CutHalf(source, localDirection, true), normal, direction, container, result);
            SpawnHalf(Art.CutHalf(source, localDirection, false), -normal, direction, container, result);

            Destroy(gameObject);
        }

        void SpawnHalf(Sprite sprite, Vector2 side, Vector2 direction, Container container, SliceResult result)
        {
            var go = new GameObject("Half");
            go.transform.SetPositionAndRotation(_body.position, _body.rotation);
            go.transform.localScale = _body.localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = _bodyRenderer.color;
            sr.sortingOrder = _bodyRenderer.sortingOrder;

            var velocity = _velocity * 0.4f + side * Random.Range(2f, 3.2f) + direction * 1.5f;
            go.AddComponent<Half>().Init(velocity, _gravity, _speed, Random.Range(-360f, 360f),
                -_game.ViewHalfHeight - 2f, container, result, Def.Color);
        }
    }
}
