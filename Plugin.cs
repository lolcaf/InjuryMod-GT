using BepInEx;
using BepInEx.Configuration;
using GorillaLocomotion;
using GorillaLocomotion.Swimming;
using InjuryMod.Classes;
using InjuryMod.MonoBehaviors;
using InjuryMod.Patches;
using InjuryMod.Utilities;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Utilla.Attributes;

namespace InjuryMod;

[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
[BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
[ModdedGamemode]
public class Plugin : BaseUnityPlugin
{
    /*
    Current ways to damage:
    Lava
    Thermal
    Drowning
    Hand Shatter
    Fall
    */
    internal static GorillaLog Log = new GorillaLog();

    public static Plugin Instance { get; private set; }

    private GRPlayerDamageEffects damageEffects;

    private int health = 100;

    private AudioSource damageSound;

    private List<string> injuries = new List<string>();

    private WaterVolume lavaVolume;

    private float lavaDamageTimer = 0f;

    private bool dead = false;

    private float thermalDamageTimer = 0f;

    private float inWaterTime = 0f;

    private float drownTimer = 0f;

    public bool inModdedRoom = false;

    public bool leftHandDisabled = false;

    public bool rightHandDisabled = false;

    private GameObject assetBundle;

    private GameObject headFollower;

    private GameObject beetleHeld;

    private GameObject beetleParticles;

    public GameObject beetlePrefab;

    public AudioSource boneBreakSFX;

    public bool holdingBeetle = false;

    private bool firstRoomJoin = true;

    private ConfigEntry<int> maxHealthConfig;

    private ConfigEntry<float> damageMultiplier;

    internal ConfigEntry<float> fallMultiplier;

    internal ConfigEntry<float> handStrength;

    private ConfigEntry<float> breathDuration;

    private ConfigEntry<bool> autoBeetle;

    private readonly List<Vector3> beetlePositions = new List<Vector3>()
    {
        new Vector3(-65.8f, 21.3f, -83.25f),
        new Vector3(-73.78f, 37.38f, -47.17f),
        new Vector3(-63.38f, 12.04f, -126.54f),
        new Vector3(-43.84f, 21.05f, -75.53f),
        new Vector3(-85.05f, 13.17f, -178.58f),
        new Vector3(-83.62f, 13.19f, -179.73f)
    };

    private void Awake()
    {
        Instance = this;
        HarmonyPatches.Patch();
        GorillaTagger.OnPlayerSpawned(() => MethodUtilities.Attempt(OnPlayerSpawned));

        maxHealthConfig = Config.Bind("General", "MaxHealth", 100, "The maximum amount of health you can have");
        damageMultiplier = Config.Bind("General", "DamageMultiplier", 1f, "The multiplier of damage when you get hurt");
        fallMultiplier = Config.Bind("Impact", "FallDamageMultiplier", 4f, "The multiplier of fall damage");
        handStrength = Config.Bind("Impact", "HandStrength", 16f, "The force you need to put to break your hand (lower = fragile)");
        breathDuration = Config.Bind("Liquid", "BreathDuration", 10f, "How long you can hold your breath in water");
        autoBeetle = Config.Bind("Cheats", "AutoBeetle", false, "Automatically give you a beetle when you pop one");
    }

    private void OnPlayerSpawned()
    {
        damageEffects = Camera.main.GetComponent<GRPlayerDamageEffects>();
        damageSound = new GameObject("DamageSFX").AddComponent<AudioSource>();
        damageSound.clip = GRPlayer.GetLocal().playerDamageSound;
        damageSound.volume = 0.4f;
        DontDestroyOnLoad(damageSound.gameObject);

        GTPlayer.Instance.playerRigidBody.AddComponent<FallDetector>();

        HandBreakDetector leftBreakDect = GTPlayer.Instance.LeftHand.controllerTransform.AddComponent<HandBreakDetector>();
        leftBreakDect.isLeftHand = true;

        HandBreakDetector rightBreakDect = GTPlayer.Instance.RightHand.controllerTransform.AddComponent<HandBreakDetector>();
        rightBreakDect.isLeftHand = false;

        assetBundle = Instantiate(AssetBundleUtilities.Load("InjuryMod.Resources.injurymodbundle", "InjuryModBundle"));

        headFollower = assetBundle.transform.Find("HeadFollower").gameObject;
        headFollower.transform.SetParent(Camera.main.transform, false);

        beetleHeld = assetBundle.transform.Find("BeetleHand").gameObject;

        beetleParticles = headFollower.transform.Find("BeetleSave").gameObject;

        beetlePrefab = assetBundle.transform.Find("BeetleCollectable").gameObject;

        boneBreakSFX = assetBundle.transform.Find("Sounds").Find("BoneBreak").GetComponent<AudioSource>();

        health = maxHealthConfig.Value;

        BeetleCollectable.InIt();
    }

    private void Update()
    {
        if (dead || !inModdedRoom) return;
        if (leftHandDisabled)
        {
            GTPlayer.Instance.GetControllerTransform(true).position = GTPlayer.Instance.headCollider.transform.position + GTPlayer.Instance.headCollider.transform.up * (-0.45f * GTPlayer.Instance.scale);
        }
        if (rightHandDisabled)
        {
            GTPlayer.Instance.GetControllerTransform(false).position = GTPlayer.Instance.headCollider.transform.position + GTPlayer.Instance.headCollider.transform.up * (-0.45f * GTPlayer.Instance.scale);
        }
        if (holdingBeetle)
        {
            beetleHeld.transform.position = VRRig.LocalRig.leftHandTransform.position;
            beetleHeld.transform.rotation = VRRig.LocalRig.leftHandTransform.rotation;
        }
        if (ZoneManagement.instance.activeZones.Contains(GTZone.VIMExperience1)) // react to the gorilla tag lava map
        {
            if (lavaVolume == null) lavaVolume = GameObject.Find("ForestLavaWaterVolume").GetComponent<WaterVolume>();
            if (lavaVolume.CheckColliderInVolume(GTPlayer.Instance.bodyCollider, out bool inWater, out bool surfaceDetected) && Time.time > lavaDamageTimer)
            {
                lavaDamageTimer = Time.time + 1f;
                DamagePlayer(20, "Severe Burns");
            }
        }
        if (Time.time > thermalDamageTimer) // react to gorilla tag's heat sources
        {
            thermalDamageTimer = Time.time + 1f;
            foreach (ThermalSourceVolume thermalSource in ThermalManager.sources)
            {
                if
                (
                    thermalSource.celsius >= 200
                    && (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, thermalSource.transform.position) <= thermalSource.outerRadius / 4
                    || Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, thermalSource.transform.position) <= thermalSource.outerRadius / 4)
                )
                {
                    DamagePlayer(Mathf.RoundToInt(thermalSource.celsius / 25), "Burns");
                }
            }
        }
        if (GTPlayer.Instance.CurrentWaterVolume != null && GTPlayer.Instance.CurrentWaterVolume.LiquidType == GTPlayer.LiquidType.Water && GTPlayer.Instance.HeadInWater)
        {
            inWaterTime += Time.deltaTime;
            if (inWaterTime > breathDuration.Value && Time.time > drownTimer)
            {
                // start drowning
                drownTimer = Time.time + 1f;
                DamagePlayer(15, "Drowning");
            }
        }
        else
        {
            drownTimer = 0f;
            if (inWaterTime > 0) inWaterTime -= Time.deltaTime * 2;
        }
    }

    [ModdedGamemodeJoin]
    private void RoomJoin()
    {
        inModdedRoom = true;
        if (holdingBeetle) beetleHeld.SetActive(true);
        else if (autoBeetle.Value) holdingBeetle = true;
        BeetleCollectable.collectableHolder.SetActive(true);
        if (firstRoomJoin)
        {
            firstRoomJoin = false;
            Log.WriteLine("First room joined");
            foreach (Vector3 pos in beetlePositions)
            {
                BeetleCollectable.Create(pos);
            }
        }
    }

    [ModdedGamemodeLeave]
    private void RoomLeft()
    {
        inModdedRoom = false;
        if (holdingBeetle) beetleHeld.SetActive(false);
        BeetleCollectable.collectableHolder.SetActive(false);
        Respawn();
    }

    public void DamagePlayer(int damage, string reason)
    {
        if (dead || !inModdedRoom) return;
        NotificationSystem.Send($"Ouch! ({Mathf.RoundToInt(damage * damageMultiplier.Value)})");
        health -= Mathf.RoundToInt(damage * damageMultiplier.Value);
        damageSound.Play();
        injuries.TryAdd(reason);
        if (health > 0)
        {
            damageEffects.radialDamageEffect.Emit(1);
            GRPlayer.GetLocal().playerDamageEffect.Emit(10);
        }
        else
        {
            StartCoroutine(PlayerDie());
        }
    }

    private string CreateDeathString()
    {
        string deathString = "";
        foreach (string injury in injuries)
        {
            deathString += injury.ToLower() + ", ";
        }
        return deathString.TrimEnd(',', ' ');
    }

    private IEnumerator PlayerDie()
    {
        if (!inModdedRoom) yield return null;

        if (holdingBeetle)
        {
            BeetleSave();
        }
        else
        {
            Log.WriteLine("You died!");
            GameObject blackout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blackout.name = "BlackScreen";
            blackout.transform.SetParent(Camera.main.transform, false);
            Destroy(blackout.GetComponent<Collider>());
            Material blackoutMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = Color.black,
            };
            blackoutMat.SetFloat("_Cull", (float)CullMode.Front);
            blackout.GetComponent<Renderer>().material = blackoutMat;

            GameObject txt = new GameObject("DeathMessage");
            txt.transform.SetParent(Camera.main.transform, false);
            txt.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            txt.transform.localRotation = Quaternion.identity;
            txt.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            TextMeshPro tmp = txt.AddComponent<TextMeshPro>();
            tmp.text = "You died to: " + CreateDeathString();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 2;

            Respawn();

            Destroy(blackout, 6f);
            Destroy(txt, 6f);

            GTPlayer.Instance.disableMovement = true;
            dead = true;
            leftHandDisabled = true;
            rightHandDisabled = true;
            yield return new WaitForSeconds(6f);
            GTPlayer.Instance.disableMovement = false;
            dead = false;
            leftHandDisabled = false;
            rightHandDisabled = false;
        }
    }

    private Coroutine setTimeActiveRoutine;

    private IEnumerator SetTimeActive(GameObject go, float time)
    {
        if (go.activeSelf) go.SetActive(false);

        go.SetActive(true);
        yield return new WaitForSeconds(time);
        go.SetActive(false);
    }

    public void StartSetTimeActive(GameObject go, float time)
    {
        if (setTimeActiveRoutine != null)
            StopCoroutine(setTimeActiveRoutine);

        setTimeActiveRoutine = StartCoroutine(SetTimeActive(go, time));
    }

    private void BeetleSave()
    {
        if (!autoBeetle.Value)
        {
            beetleHeld.SetActive(false);
            holdingBeetle = false;
        }
        leftHandDisabled = false;
        rightHandDisabled = false;
        Respawn();
        StartSetTimeActive(beetleParticles, 4f);
    }

    public void GiveBeetle()
    {
        if (holdingBeetle) return;
        beetleHeld.SetActive(true);
        holdingBeetle = true;
        VRRig.LocalRig.PlayHandTapLocal(20, false, 2f);
    }

    private void Respawn()
    {
        health = maxHealthConfig.Value;
        injuries.Clear();
        inWaterTime = 0f;
    }
}
