using System;
using UnityEditor;
using UnityEngine;
using GyaruRogue.Generation;

public static class DistrictGenerationChecks
{
    [MenuItem("Gyaru Rogue/Comprobar 1000 mapas")]
    public static void CheckSeeds()
    {
        try
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                DistrictPlan plan = DistrictGenerator.Generate(seed,1+seed%3,4+seed%5,1+seed%6);
                if (plan.Validate() != null || plan.Fingerprint() != DistrictGenerator.Generate(seed,1+seed%3,4+seed%5,1+seed%6).Fingerprint())
                    throw new InvalidOperationException("Fallo en semilla " + seed);
            }
            Debug.Log("1000 mapas: conectividad y repeticion comprobadas.");
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    [MenuItem("Gyaru Rogue/Comprobar pausa en Play Mode")]
    public static void CheckPause()
    {
        GyaruDemoBootstrap demo = UnityEngine.Object.FindObjectOfType<GyaruDemoBootstrap>();
        if (!Application.isPlaying || demo == null || demo.IsModalOpen || demo.Health.IsDead)
        { Debug.LogWarning("Ejecuta la partida y cierra los dialogos primero."); return; }
        float scale = Time.timeScale; bool audio = AudioListener.pause;
        Rigidbody2D body = demo.Player.GetComponent<Rigidbody2D>(); Vector2 velocity = body.linearVelocity;
        try
        {
            demo.OpenPauseMenu();
            if (!demo.IsPaused || Time.timeScale != 0 || !AudioListener.pause || body.linearVelocity != velocity)
                throw new InvalidOperationException("La pausa no conserva el estado esperado.");
            GyaruDemoUI ui=demo.GetComponentInChildren<GyaruDemoUI>();
            ui.ShowSettings(); ui.Back(); ui.ShowMap(); ui.Back(); ui.ShowSlots(true); ui.Back();
            if (Time.timeScale != 0 || !AudioListener.pause) throw new InvalidOperationException("Una subpantalla reanudó el juego.");
            demo.CloseModal();
            if (demo.IsPaused || Time.timeScale != scale || AudioListener.pause != audio)
                throw new InvalidOperationException("La pausa no restauro tiempo o audio.");
            Debug.Log("Pausa: tiempo, audio, velocidad y navegacion comprobados.");
        }
        catch (Exception error) { Debug.LogException(error); }
        finally { demo.CloseModal(); Time.timeScale = scale; AudioListener.pause = audio; }
    }
    [MenuItem("Gyaru Rogue/Comprobar distrito en Play Mode")]
    public static void CheckLiveDistrict()
    {
        DistrictRuntime district = UnityEngine.Object.FindObjectOfType<DistrictRuntime>();
        if (!Application.isPlaying || district == null) { Debug.LogWarning("Abre DistritoProcedural y pulsa Play primero."); return; }
        try
        {
            string error = district.Plan.Validate();
            if (error != null) throw new InvalidOperationException(error);
            int oneWay = 0;
            foreach (BoxCollider2D collider in district.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (!collider.usedByEffector) continue;
                PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>();
                if (effector == null || !effector.useOneWay || !effector.useOneWayGrouping)
                    throw new InvalidOperationException("Pasarela sin configuracion descendible.");
                oneWay++;
            }
            if (oneWay == 0) throw new InvalidOperationException("Faltan las pasarelas.");
            foreach (SpriteRenderer renderer in district.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite != null && renderer.sharedMaterial == null)
                    throw new InvalidOperationException("Sprite sin material: " + renderer.name);
            }
            Debug.Log("Distrito activo: " + district.Plan.Rooms.Count + " salas, " + oneWay + " pasarelas y materiales comprobados. Codigo: " + district.Plan.Fingerprint());
        }
        catch (Exception error) { Debug.LogException(error); }
    }
}
