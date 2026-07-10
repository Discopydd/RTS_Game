using UnityEngine;
using UnityEngine.Rendering;

public class DeathEffect : MonoBehaviour
{
    public static void Create(Vector3 position, UnitTeam team)
    {
        GameObject effectObject = new GameObject("DeathEffect");
        effectObject.transform.position = position + Vector3.up * 0.8f; // Adjust the position slightly above the ground

        ParticleSystem particleSystem = effectObject.AddComponent<ParticleSystem>();

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.45f;
        main.startSpeed = 3.5f;
        main.startSize = 0.25f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        if(team == UnitTeam.Player)
        {
            main.startColor = new Color(0.2f, 0.6f, 1f, 1f);
        }
        else if(team == UnitTeam.Enemy)
        {
            main.startColor = new Color(1f, 0.2f, 0.1f, 1f);
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, 24);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            Material material = new Material(shader);
            renderer.material = material;
        }

        particleSystem.Play();

        Destroy(effectObject, 1.0f);
    }
}
