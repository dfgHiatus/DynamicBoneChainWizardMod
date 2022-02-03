using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using NeosModLoader;
using HarmonyLib;
using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using System.IO;

namespace Wizard
{
    public class Wizard : NeosMod
    {
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> INTERIA = new ModConfigurationKey<float>("inertia", "inertia's dynamic bone config", () => 0.2f);
        public static ModConfigurationKey<float> INTERTIA_FORCE = new ModConfigurationKey<float>("inertia_force", "inertia_force's dynamic bone config", () => 2.0f);
        public static ModConfigurationKey<float> DAMPING = new ModConfigurationKey<float>("damping", "damping's dynamic bone config", () => 5.0f);
        public static ModConfigurationKey<float> ELASTICITY = new ModConfigurationKey<float>("elasticity", "elasticity's dynamic bone config", () => 100f);
        public static ModConfigurationKey<float> STIFFNESS = new ModConfigurationKey<float>("stiffness", "stiffness's dynamic bone config", () => 0.2f);
        public static ModConfigurationKey<bool> IS_GRABBABLE = new ModConfigurationKey<bool>("isGrabbable", "isGrabbable's dynamic bone config", () => false);
        public static readonly ModConfigurationKey<bool> AutoSaveConfig = 
            new ModConfigurationKey<bool>("AutoSaveConfig", "If true the Config gets saved after every change.", () => true);
        public static readonly ModConfigurationKey<bool> ConfigFileExist = 
            new ModConfigurationKey<bool>("ConfigFileExist", "Value to check if the config file need to be initialized.", () => false, true);
        public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "DynamicBoneWizard Enabled.", () => true);

		public static ValueField<bool> IgnoreInactive;
		public static ValueField<bool> IgnoreDisabled;
		public static ValueField<bool> IgnoreNonPersistent;
		public static ValueField<float> Inertia;
		public static ValueField<float> InertiaForce;
		public static ValueField<float> Damping;
		public static ValueField<float> Elasticity;
		public static ValueField<float> Stiffness;
		public static ValueField<bool> IsGrabbable;

		public static ModConfiguration config;
        private static bool _enabled = true;

        public override string Name => "DynamicBoneChainWizardMod";
        public override string Author => "dfgHiatus";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/DynamicBoneChainWizardMod";

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            config.OnThisConfigurationChanged += RefreshConfigState;
            Harmony harmony = new Harmony("net.dfgHiatus.DynamicBoneChainWizardMod");
            harmony.PatchAll();
        }

		private void RefreshConfigState(ConfigurationChangedEvent configurationChangedEvent = null)
		{
			_enabled = config.GetValue(Enabled);
			Inertia.Value.Value = config.GetValue(INTERIA);
			InertiaForce.Value.Value = config.GetValue(INTERTIA_FORCE);
			Damping.Value.Value = config.GetValue(DAMPING);
			Elasticity.Value.Value = config.GetValue(ELASTICITY);
			Stiffness.Value.Value = config.GetValue(STIFFNESS);
			IsGrabbable.Value.Value = config.GetValue(IS_GRABBABLE);

			if (config.GetValue(AutoSaveConfig) || Equals(configurationChangedEvent?.Key, AutoSaveConfig))
			{
				config.Save(true);
			}
		}

		[HarmonyPatch(typeof(NeosSwapCanvasPanel), "OnAttach")]
		public class DevCreateNewTesting
        {
            public static void Postfix(NeosSwapCanvasPanel __instance)
            {
                DevCreateNewForm createForm = __instance.Slot.GetComponent<DevCreateNewForm>();
                if (createForm == null)
                {
                    return;
                }
                createForm.Slot.GetComponentInChildren<Canvas>().Size.Value = new float2(200f, 700f);
                SyncRef<RectTransform> rectTransform = (SyncRef<RectTransform>)AccessTools.Field(typeof(NeosSwapCanvasPanel), "_currentPanel").GetValue(__instance);
                rectTransform.OnTargetChange += RectTransform_OnTargetChange;
            }
			public static void RectTransform_OnTargetChange(SyncRef<RectTransform> reference)
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
					if (config.GetValue(Enabled))
					{
						buttonSlot.GetComponentInChildren<Text>().Content.Value = "Dynamic Bone Chain Wizard";
					}
					else
					{
						buttonSlot.GetComponentInChildren<Text>().Content.Value = "[No Config was found for Dynamic Bone Chain Wizard]";
					}

                    buttonSlot.GetComponent<ButtonRelay<string>>().Destroy();
                    Button button = buttonSlot.GetComponent<Button>();
                    button.LocalPressed += Button_LocalPressed;
                });
            }
            public static void Button_LocalPressed(IButton button, ButtonEventData eventData)
            {
				if (config.GetValue(Enabled))
				{
					DynamicBoneWizard wiz = new DynamicBoneWizard();
					button.Slot.GetObjectRoot().Destroy();
				}
            }

		public class DynamicBoneWizard
		{
				// Psuedo-enum
				Dictionary<string, int> UseTagMode = new Dictionary<string, int>(){
					{ "IgnoreTag", 1 },
					{ "IncludeOnlyWithTag", 2},
					{ "ExcludeAllWithTag", 3}
				};

				public static DynamicBoneWizard _Wizard;
				public static Slot WizardSlot;

				// DynBoneWizard Stuff
				public List<string> listOfFullNames = new List<string>();

				public List<string> listOfPrefixes = new List<string>();
				public List<string> listOfSuffixes = new List<string>();
				public List<string> listOfSingletons = new List<string>();

				public ValueField<int> useTagMode;
				public ReferenceField<Slot> ProcessRoot;
				public readonly TextField tag;
				public readonly Text resultsText;
				private int _count;
				private color _buttonColor;
				private LocaleString _text;
				private Slot _scrollAreaRoot;
				private UIBuilder _listBuilder;

				public DynamicBoneWizard()
				{
					UniLog.Log("Before Construction");
					_Wizard = this;
					UniLog.Log("Start slots");
					WizardSlot = Engine.Current.WorldManager.FocusedWorld.RootSlot.AddSlot("Dynamic Bone Wizard");
					WizardSlot.GetComponentInChildrenOrParents<Canvas>()?.MarkDeveloper();
					WizardSlot.PersistentSelf = false;
					UniLog.Log("End slots");

					UniLog.Log("Start Data");
					Slot Data = WizardSlot.AddSlot("Data");
					IgnoreInactive = Data.AddSlot("IgnoreInactive").AttachComponent<ValueField<bool>>();
					IgnoreInactive.Value.Value = true;
					IgnoreDisabled = Data.AddSlot("IgnoreDisabled").AttachComponent<ValueField<bool>>();
					IgnoreDisabled.Value.Value = true;
					IgnoreNonPersistent = Data.AddSlot("IgnoreNonPersistent").AttachComponent<ValueField<bool>>();
					IgnoreNonPersistent.Value.Value = true;
					Inertia = Data.AddSlot("Inertia").AttachComponent<ValueField<float>>();
					Inertia.Value.Value = config.GetValue(INTERIA);
					InertiaForce = Data.AddSlot("InertiaForce").AttachComponent<ValueField<float>>();
					InertiaForce.Value.Value = config.GetValue(INTERTIA_FORCE);
					Damping = Data.AddSlot("Damping").AttachComponent<ValueField<float>>();
					Damping.Value.Value = config.GetValue(DAMPING);
					Elasticity = Data.AddSlot("Elasticity").AttachComponent<ValueField<float>>();
					Elasticity.Value.Value = config.GetValue(ELASTICITY);
					Stiffness = Data.AddSlot("Stiffness").AttachComponent<ValueField<float>>();
					Stiffness.Value.Value = config.GetValue(STIFFNESS);
					IsGrabbable = Data.AddSlot("IsGrabbable").AttachComponent<ValueField<bool>>();
					IsGrabbable.Value.Value = config.GetValue(IS_GRABBABLE);

					useTagMode = Data.AddSlot("useTagMode").AttachComponent<ValueField<int>>();
					useTagMode.Value.Value = UseTagMode["IgnoreTag"];
					ProcessRoot = Data.AddSlot("WizardSlot").AttachComponent<ReferenceField<Slot>>();
					ProcessRoot.Reference.Target = null;
					UniLog.Log("End Data");

					// We're assuming all files are accounted for
					UniLog.Log("Start list 1");
					var logFile = File.ReadAllLines(Path.Combine("nml_mods", "_BoneLists", "listOfSingletons.txt"));
					foreach (var s in logFile)
						listOfSingletons.Add(s);
					UniLog.Log("End list 1");

					UniLog.Log("Start list 2");
					logFile = File.ReadAllLines(Path.Combine("nml_mods", "_BoneLists", "listOfPrefixes.txt"));
					foreach (var s in logFile)
						listOfPrefixes.Add(s);
					UniLog.Log("End list 2");

					UniLog.Log("Start list 3");
					logFile = File.ReadAllLines(Path.Combine("nml_mods", "_BoneLists", "listOfSuffixes.txt"));
					foreach (var s in logFile)
						listOfPrefixes.Add(s);
					UniLog.Log("End list 3");

					UniLog.Log("Start generate lists");
					GenerateFullNamesList();
					UniLog.Log("End generate lists");

					UniLog.Log("Start UI");
					// Create the UI for the wizard
					WizardSlot.Name = "DynamicBoneChain Management Wizard";
					WizardSlot.Tag = "Developer";
					NeosCanvasPanel neosCanvasPanel = WizardSlot.AttachComponent<NeosCanvasPanel>();
					neosCanvasPanel.Panel.AddCloseButton();
					neosCanvasPanel.Panel.AddParentButton();
					neosCanvasPanel.Panel.Title = "DynamicBoneChain Management Wizard";
					neosCanvasPanel.CanvasSize = new float2(800f, 900f);
					UIBuilder uIBuilder = new UIBuilder(neosCanvasPanel.Canvas);
					List<RectTransform> rectList = uIBuilder.SplitHorizontally(0.5f, 0.5f);
					UniLog.Log("End UI");

					UniLog.Log("Start Settings");
					// Build left hand side UI - options and buttons.
					UIBuilder uIBuilder2 = new UIBuilder(rectList[0].Slot);
					Slot _layoutRoot = uIBuilder2.VerticalLayout(4f, 0f, new Alignment()).Slot;
					uIBuilder2.FitContent(SizeFit.Disabled, SizeFit.MinSize);
					uIBuilder2.Style.Height = 24f;
					UIBuilder uIBuilder3 = uIBuilder2;
					UniLog.Log("End Settings");

					UniLog.Log("Start Armature");
					// Slot reference to which changes will be applied.
					_text = "Armature slot:";
					uIBuilder3.Text(in _text);
					uIBuilder3.Next("Root");
					uIBuilder3.Current.AttachComponent<RefEditor>().Setup(ProcessRoot.Reference);
					uIBuilder3.Spacer(24f);
					UniLog.Log("End Armature");

					UniLog.Log("Start Settings 2");
					// Basic filtering settings for which DynamicBoneChain are accepted for changes or listing.
					_text = "Exclude Inactive:";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.9f, () => uIBuilder3.BooleanMemberEditor(IgnoreInactive.Value));
					_text = "Exclude Disabled:";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.9f, () => uIBuilder3.BooleanMemberEditor(IgnoreDisabled.Value));
					_text = "Exclude Non-persistent:";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.9f, () => uIBuilder3.BooleanMemberEditor(IgnoreNonPersistent.Value));
					_text = "Tag:";
					tag = uIBuilder3.HorizontalElementWithLabel(in _text, 0.2f, () => uIBuilder3.TextField());
					_text = "Tag handling mode:";
					uIBuilder3.Text(in _text);
					uIBuilder3.EnumMemberEditor(useTagMode.Value);
					uIBuilder3.Spacer(24f);
					UniLog.Log("End Settings 2");

					UniLog.Log("Start Settings 3");
					// Dynamic Bone Settings
					_text = "Elasticity";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(Elasticity.Value));
					_text = "Stiffness";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(Stiffness.Value));
					_text = "Inertia";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(Inertia.Value));
					_text = "InertiaForce";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(InertiaForce.Value));
					_text = "Damping";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(Damping.Value));
					_text = "Grabbable";
					uIBuilder3.HorizontalElementWithLabel(in _text, 0.9f, () => uIBuilder3.BooleanMemberEditor(IsGrabbable.Value));
					uIBuilder3.Spacer(24f);
					UniLog.Log("End Settings 3");

					UniLog.Log("Start Settings 4");
					// Buttons for batch actions.
					_text = "List matching DynamicBoneChains";
					uIBuilder3.Button(in _text).LocalPressed += PopulateList;
					_text = "Remove all DynamicBoneChains";
					uIBuilder3.Button(in _text).LocalPressed += RemoveAllDynamicBoneChains;
					_text = "Attach DynamicBoneChains";
					uIBuilder3.Button(in _text).LocalPressed += AttachDynamicBones;
					_text = "Reload Dynamic Bone Settings";
					uIBuilder3.Button(in _text).LocalPressed += RefreshConfigStateProxy;
					_text = "Save Dynamic Bone Settings";
					uIBuilder3.Button(in _text).LocalPressed += SaveSettings;
					uIBuilder3.Spacer(24f);
					resultsText = uIBuilder3.Text(in _text);
					UniLog.Log("End Settings 4");

					UniLog.Log("Start Settings 5");
					// Build right hand side UI - list of found DynamicBoneChain.
					UIBuilder uIBuilder4 = new UIBuilder(rectList[1].Slot);
					uIBuilder4.ScrollArea();
					uIBuilder4.VerticalLayout(10f, 4f);
					_scrollAreaRoot = uIBuilder4.FitContent(SizeFit.Disabled, SizeFit.MinSize).Slot;
					UniLog.Log("End Settings 5");

					UniLog.Log("Start Settings 6");
					// Prepare UIBuilder for addding elements to DynamicBoneChain list.
					_listBuilder = uIBuilder4;
					_listBuilder.Style.MinHeight = 40f;
					UniLog.Log("End Settings 6");

					WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
					UniLog.Log("After Construction");
				}

				private void RefreshConfigStateProxy(IButton button, ButtonEventData eventData) 
				{
					RefreshConfigState();
				}

				private void RefreshConfigState(ConfigurationChangedEvent configurationChangedEvent = null)
				{
					_enabled = config.GetValue(Enabled);
					Inertia.Value.Value = config.GetValue(INTERIA);
					InertiaForce.Value.Value = config.GetValue(INTERTIA_FORCE);
					Damping.Value.Value = config.GetValue(DAMPING);
					Elasticity.Value.Value = config.GetValue(ELASTICITY);
					Stiffness.Value.Value = config.GetValue(STIFFNESS);
					IsGrabbable.Value.Value = config.GetValue(IS_GRABBABLE);

					if (config.GetValue(AutoSaveConfig) || Equals(configurationChangedEvent?.Key, AutoSaveConfig))
					{
						config.Save(true);
					}
				}

				private void SaveSettings(IButton button, ButtonEventData eventData)
				{
					config.Save(true);
				}

				private void AttachDynamicBones(IButton button, ButtonEventData eventData)
				{
					// Check whether DynamicBoneChain should be filtered out.
					// If parent has a dynamic bone chain
					// If the slot's name isn't interesting enough
					// If the user says so (by tag, persistent, active)

					WizardSlot.RunSynchronously(() =>
					{
						if (ProcessRoot != null)
						{
							_count = 0;
							recursion(ProcessRoot.Reference.Target);

							PopulateList();
							ShowResults(_count + " DynamicBoneChains attached!");

						}
						else ShowResults("No target root slot set.");
					});
				}

				private void recursion(Slot slot)
				{
					for (int i = 0; i < slot.ChildrenCount; i++)
					{
						if (NameCheck(slot[i].Name.ToLower())
						&& (slot[i].GetComponent<Wiggler>() == null)
						&& !slot[i].Name.Contains("Wiggler")
						&& !slot[i].Name.Contains("Button")
						&& (!IgnoreInactive.Value.Value || slot[i].IsActive)
						&& (!IgnoreNonPersistent.Value.Value || slot[i].IsPersistent)
						&&
						((useTagMode.Value.Value == UseTagMode["IgnoreTag"])
						|| (useTagMode.Value.Value == UseTagMode["IncludeOnlyWithTag"] && slot[i].Tag == tag.TargetString)
						|| (useTagMode.Value.Value == UseTagMode["ExcludeAllWithTag"] && slot[i].Tag != tag.TargetString)))
						{
							var boneChain = slot[i].AttachComponent<DynamicBoneChain>();
							boneChain.Elasticity.Value = Elasticity.Value;
							boneChain.Stiffness.Value = Stiffness.Value;
							boneChain.Inertia.Value = Inertia.Value;
							boneChain.InertiaForce.Value = InertiaForce.Value;
							boneChain.Damping.Value = Damping.Value;
							boneChain.IsGrabbable.Value = IsGrabbable.Value;
							boneChain.SetupFromChildren(slot[i]);
							slot[i].Tag = "boneAdded";
							_count++;
						}
						else
						{
							recursion(slot[i]);
						}
					}
				}

				private void GenerateFullNamesList()
				{
					listOfFullNames.Clear();

					foreach (var singleton in listOfSingletons)
					{
						listOfFullNames.Add(singleton);
					}

					foreach (var prefix in listOfPrefixes)
					{
						foreach (var suffix in listOfSuffixes)
						{
							listOfFullNames.Add(prefix + suffix);
						}
					}
				}

				private bool NameCheck(string slotCanidateName)
				{
					foreach (var item in listOfFullNames)
					{
						if (slotCanidateName.StartsWith(item))
						{
							return true;

							/* Dated 
							bool lengthCheck = true;
							for (int len = 0; len < item.Length; len++)
							{
								if (name[len] != item[len])
								{
									lengthCheck = false;
								}
							}
							if (lengthCheck)
							{
								return true;
							}*/
						}
					}
					return false;
				}

				private bool ParentTagCheck(Slot child)
				{
					var boneList = new List<DynamicBoneChain>();
					child.GetComponentsInParents<DynamicBoneChain>(boneList);

					// Implies there are no parents with dynamic bones above this slot
					if (boneList.Count == 0)
					{
						return true;
					}
					else
					{
						return false;
					}

				}

				private void CreateScrollListElement(DynamicBoneChain mc)
				{
					Slot _elementRoot = _listBuilder.Next("Element");
					var _refField = _elementRoot.AttachComponent<ReferenceField<DynamicBoneChain>>();
					_refField.Reference.Target = mc;
					UIBuilder _listBuilder2 = new UIBuilder(_elementRoot);
					_listBuilder2.NestInto(_elementRoot);
					_listBuilder2.VerticalLayout(4f, 4f);
					_listBuilder2.HorizontalLayout(10f);
					// Dated?
					_buttonColor = new color(1f, 1f, 1f);
					_listBuilder2.NestOut();
					_listBuilder2.NestOut();
					_listBuilder2.Current.AttachComponent<RefEditor>().Setup(_refField.Reference);
				}

				private void GetAllDynamicBones(Action<DynamicBoneChain> process)
				{
					if (ProcessRoot != null)
					{
						foreach (DynamicBoneChain componentsInChild in ProcessRoot.Reference.Target.GetComponentsInChildren<DynamicBoneChain>(delegate (DynamicBoneChain mc)
						{
							return (!IgnoreInactive.Value.Value || mc.Slot.IsActive)
							&& (!IgnoreDisabled.Value.Value || mc.Enabled)
							&& (!IgnoreNonPersistent.Value.Value || mc.IsPersistent)
							&& ((useTagMode.Value.Value == UseTagMode["IgnoreTag"])
							|| (useTagMode.Value.Value == UseTagMode["IncludeOnlyWithTag"] && mc.Slot.Tag == tag.TargetString)
							|| (useTagMode.Value.Value == UseTagMode["ExcludeAllWithTag"] && mc.Slot.Tag != tag.TargetString));
						}))
						{
							process(componentsInChild);
						}
					}
					else ShowResults("No target root slot set.");
				}

				private void PopulateList()
				{
					_scrollAreaRoot.DestroyChildren();
					GetAllDynamicBones(delegate (DynamicBoneChain mc)
					{
						CreateScrollListElement(mc);
					});
				}

				private void PopulateList(IButton button, ButtonEventData eventData)
				{
					_count = 0;
					_scrollAreaRoot.DestroyChildren();
					GetAllDynamicBones(delegate (DynamicBoneChain mc)
					{
						CreateScrollListElement(mc);
						_count++;
					});
					ShowResults(_count + " matching DynamicBoneChain(s) listed.");
				}

				private void RemoveAllDynamicBoneChains(IButton button, ButtonEventData eventData)
				{
					List<Slot> tagList = new List<Slot>();
					ProcessRoot.Reference.Target.GetAllChildren(tagList);

					int _count = 0;
					foreach (var slot in tagList)
					{
						DynamicBoneChain boneChain = slot.GetComponent<DynamicBoneChain>();
						if (boneChain != null)
						{
							boneChain.Enabled = false;
							boneChain.Destroy();
							// Implies exsistence of "boneRemoved"
							slot.Tag = string.Empty;
							_count++;
						}
					}

					PopulateList();
					ShowResults($"{_count} DynamicBoneChain(s) deleted.");
				}

				private void ShowResults(string results)
				{
					resultsText.Content.Value = results;
				}
			}
		}
    }
}
