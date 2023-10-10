using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResoniteModLoader;
using HarmonyLib;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;

namespace Wizard;

public class Wizard : ResoniteMod
{
    public override string Name => "DynamicBoneChainWizardMod";
    public override string Author => "dfgHiatus";
    public override string Version => "2.0.0";
    public override string Link => "https://github.com/dfgHiatus/DynamicBoneChainWizardMod";

    public override void OnEngineInit()
    {
        new Harmony("net.dfgHiatus.DynamicBoneChainWizardMod").PatchAll(); ;
    }

    [HarmonyPatch(typeof(LegacySwapCanvasPanel), "OnAttach")]
    class DevCreateNewTesting
    {
        public static void Postfix(LegacySwapCanvasPanel __instance)
        {
            DevCreateNewForm createForm = __instance.Slot.GetComponent<DevCreateNewForm>();
            if (createForm == null)
            {
                return;
            }
            createForm.Slot.GetComponentInChildren<Canvas>().Size.Value = new float2(200f, 700f);
            SyncRef<RectTransform> rectTransform = 
                (SyncRef<RectTransform>)AccessTools.Field(typeof(LegacySwapCanvasPanel), "_currentPanel").GetValue(__instance);
            rectTransform.OnTargetChange += RectTransform_OnTargetChange;
        }
        static void RectTransform_OnTargetChange(SyncRef<RectTransform> reference)
        {
            Engine.Current.WorldManager.FocusedWorld.Coroutines.StartTask(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Engine.Current.WorldManager.FocusedWorld.Time.Delta + 0.01f)).ConfigureAwait(continueOnCapturedContext: false);
                await default(ToWorld);

                List<Text> texts = reference.Target.Slot.GetComponentsInChildren<Text>();

                if (texts[0] == null)
                {
                    return;
                }
                if (!texts[0].Content.Value.Contains("3D"))
                {
                    return;
                }

                Slot buttonSlot = texts[8].Slot.Parent.Duplicate();
                buttonSlot.GetComponentInChildren<Text>().Content.Value = "Dynamic Bone Chain Wizard";
                buttonSlot.GetComponent<ButtonRelay<string>>().Destroy();

                Button button = buttonSlot.GetComponent<Button>();
                button.LocalPressed += Button_LocalPressed;
            });
        }
        static void Button_LocalPressed(IButton button, ButtonEventData eventData)
        {
            new DynamicBoneWizard();
            button.Slot.GetObjectRoot().Destroy();
        }
    }
}
