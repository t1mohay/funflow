using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Процедурные звуки-заглушки: генерируются в коде, файлов не нужно.
    /// Позже можно заменить на AudioClip из Resources, интерфейс останется тем же.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        AudioSource[] _sources;
        int _next;
        AudioClip _swoosh, _chop, _good, _overflow, _bad, _tick, _go, _complete, _lose, _click, _launch;

        void Awake()
        {
            _sources = new AudioSource[12];
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }

            _swoosh = MakeSwoosh();
            _chop = MakeChop();
            _good = MakeGood();
            _overflow = MakeSweep("overflow", 0.16f, 240f, 150f, 20f);
            _bad = MakeBuzz();
            _tick = MakeTone("tick", 0.06f, 1000f, 60f);
            _go = MakeSweep("go", 0.25f, 880f, 1760f, 8f);
            _complete = MakeArpeggio("complete", new[] { 523f, 659f, 784f, 1046f }, 0.09f);
            _lose = MakeArpeggio("lose", new[] { 392f, 330f, 262f }, 0.2f);
            _click = MakeTone("click", 0.035f, 1500f, 80f);
            _launch = MakeSweep("launch", 0.09f, 180f, 420f, 25f);
        }

        public void PlaySwoosh() => Play(_swoosh, 0.3f, 0.15f);
        public void PlayChop() => Play(_chop, 0.55f, 0.12f);
        /// <param name="fill">0..1 — насколько заполнен ингредиент, тон растёт к концу.</param>
        public void PlayGood(float fill) => Play(_good, 0.4f, 0.02f, 1f + 0.3f * fill);
        public void PlayOverflow() => Play(_overflow, 0.5f, 0.05f);
        public void PlayBad() => Play(_bad, 0.45f, 0.03f);
        public void PlayTick() => Play(_tick, 0.4f, 0f);
        public void PlayGo() => Play(_go, 0.4f, 0f);
        public void PlayComplete() => Play(_complete, 0.45f, 0f);
        public void PlayLose() => Play(_lose, 0.45f, 0f);
        public void PlayClick() => Play(_click, 0.35f, 0.05f);
        public void PlayLaunch() => Play(_launch, 0.12f, 0.2f);

        void Play(AudioClip clip, float volume, float pitchJitter, float pitch = 1f)
        {
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        static AudioClip MakeSwoosh()
        {
            int n = Samples(0.22f);
            var data = new float[n];
            var rng = new System.Random(1);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float p = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * p);
                float cut = Mathf.Lerp(0.3f, 0.05f, p);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp1 += (noise - lp1) * cut;
                lp2 += (lp1 - lp2) * cut;
                data[i] = lp2 * env * env;
            }
            return Clip("swoosh", data);
        }

        static AudioClip MakeChop()
        {
            int n = Samples(0.15f);
            var data = new float[n];
            var rng = new System.Random(2);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f);
                phase += TwoPi * Mathf.Lerp(190f, 90f, t / 0.15f) / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 30f);
                data[i] = noise * 0.6f + thump;
            }
            return Clip("chop", data);
        }

        static AudioClip MakeGood()
        {
            int n = Samples(0.2f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool second = t >= 0.06f;
                float f = second ? 1175f : 784f;
                float local = second ? t - 0.06f : t;
                phase += TwoPi * f / Rate;
                data[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * Mathf.Exp(-local * 20f);
            }
            return Clip("good", data);
        }

        static AudioClip MakeBuzz()
        {
            int n = Samples(0.32f);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float p = i / (float)n;
                float sq = Mathf.Sign(Mathf.Sin(TwoPi * 118f * t)) + Mathf.Sign(Mathf.Sin(TwoPi * 125f * t));
                lp += (sq - lp) * 0.25f;
                data[i] = lp * Mathf.Pow(1f - p, 1.5f);
            }
            return Clip("bad", data);
        }

        static AudioClip MakeTone(string name, float duration, float freq, float decay)
        {
            int n = Samples(duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t * decay);
            }
            return Clip(name, data);
        }

        static AudioClip MakeSweep(string name, float duration, float from, float to, float decay)
        {
            int n = Samples(duration);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += TwoPi * Mathf.Lerp(from, to, i / (float)n) / Rate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-t * decay);
            }
            return Clip(name, data);
        }

        static AudioClip MakeArpeggio(string name, float[] notes, float step)
        {
            float duration = step * (notes.Length - 1) + 0.35f;
            int n = Samples(duration);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Samples(step * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    data[i] += Mathf.Sin(TwoPi * notes[k] * t) * Mathf.Exp(-t * 9f);
                }
            }
            return Clip(name, data);
        }

        static int Samples(float seconds) => Mathf.CeilToInt(seconds * Rate);

        static AudioClip Clip(string name, float[] data)
        {
            float max = 0.0001f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float gain = 0.9f / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
