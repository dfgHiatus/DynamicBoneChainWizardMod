using System;
using System.Collections.Generic;
using System.IO;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;

namespace Wizard;

public class DynamicBoneWizard
{
    // Psuedo-enum
    private readonly Dictionary<string, int> UseTagMode = new(){
		{ "IgnoreTag", 1 },
		{ "IncludeOnlyWithTag", 2},
		{ "ExcludeAllWithTag", 3}
	};

	private readonly Slot WizardSlot;

    private readonly List<string> listOfFullNames = new();

	private readonly List<string> listOfPrefixes = new();
	private readonly List<string> listOfSuffixes = new();
	private readonly List<string> listOfSingletons = new();

	private readonly ValueField<bool> IgnoreInactive;
	private readonly ValueField<bool> IgnoreDisabled;
	private readonly ValueField<bool> IgnoreNonPersistent;
	private readonly ValueField<float> Inertia;
	private readonly ValueField<float> InertiaForce;
	private readonly ValueField<float> Damping;
	private readonly ValueField<float> Elasticity;
	private readonly ValueField<float> Stiffness;
	private readonly ValueField<bool> IsGrabbable;

	private readonly ValueField<int> useTagMode;
	private readonly ReferenceField<Slot> ProcessRoot;
	public readonly TextField tag;
	public readonly Text resultsText;
	private int _count;
    private readonly LocaleString _text;
	private readonly Slot _scrollAreaRoot;
	private readonly UIBuilder _listBuilder;

	public DynamicBoneWizard()
	{
		WizardSlot = Engine.Current.WorldManager.FocusedWorld.RootSlot.AddSlot("Dynamic Bone Wizard");
		WizardSlot.GetComponentInChildrenOrParents<Canvas>()?.MarkDeveloper();
		WizardSlot.PersistentSelf = false;

		Slot Data = WizardSlot.AddSlot("Data");
		IgnoreInactive = Data.AddSlot("IgnoreInactive").AttachComponent<ValueField<bool>>();
		IgnoreInactive.Value.Value = true;
		IgnoreDisabled = Data.AddSlot("IgnoreDisabled").AttachComponent<ValueField<bool>>();
		IgnoreDisabled.Value.Value = true;
		IgnoreNonPersistent = Data.AddSlot("IgnoreNonPersistent").AttachComponent<ValueField<bool>>();
		IgnoreNonPersistent.Value.Value = true;
		Inertia = Data.AddSlot("Inertia").AttachComponent<ValueField<float>>();
		Inertia.Value.Value = 0.2f;
		InertiaForce = Data.AddSlot("InertiaForce").AttachComponent<ValueField<float>>();
		InertiaForce.Value.Value = 2f;
		Damping = Data.AddSlot("Damping").AttachComponent<ValueField<float>>();
		Damping.Value.Value = 5;
		Elasticity = Data.AddSlot("Elasticity").AttachComponent<ValueField<float>>();
		Elasticity.Value.Value = 100f;
		Stiffness = Data.AddSlot("Stiffness").AttachComponent<ValueField<float>>();
		Stiffness.Value.Value = 0.2f;
		IsGrabbable = Data.AddSlot("IsGrabbable").AttachComponent<ValueField<bool>>();
		IsGrabbable.Value.Value = false;

		useTagMode = Data.AddSlot("useTagMode").AttachComponent<ValueField<int>>();
		useTagMode.Value.Value = UseTagMode["IgnoreTag"];
		ProcessRoot = Data.AddSlot("WizardSlot").AttachComponent<ReferenceField<Slot>>();
		ProcessRoot.Reference.Target = null;

		// We're assuming all files are accounted for
		var logFile = File.ReadAllLines(Path.Combine("rml_mods", "_BoneLists", "listOfSingletons.txt"));
		foreach (var s in logFile)
			listOfSingletons.Add(s);

		logFile = File.ReadAllLines(Path.Combine("rml_mods", "_BoneLists", "listOfPrefixes.txt"));
		foreach (var s in logFile)
			listOfPrefixes.Add(s);

		logFile = File.ReadAllLines(Path.Combine("rml_mods", "_BoneLists", "listOfSuffixes.txt"));
		foreach (var s in logFile)
			listOfSuffixes.Add(s);

		GenerateFullNamesList();

		// Create the UI for the wizard
		WizardSlot.Name = "DynamicBoneChain Management Wizard";
		WizardSlot.Tag = "Developer";
		LegacyCanvasPanel resoniteCanvasPanel = WizardSlot.AttachComponent<LegacyCanvasPanel>();
		resoniteCanvasPanel.Panel.AddCloseButton();
		resoniteCanvasPanel.Panel.AddParentButton();
		resoniteCanvasPanel.Panel.Title = "DynamicBoneChain Management Wizard";
		resoniteCanvasPanel.CanvasSize = new float2(800f, 900f);
		UIBuilder uIBuilder = new(resoniteCanvasPanel.Canvas);
		List<RectTransform> rectList = uIBuilder.SplitHorizontally(0.5f, 0.5f);

		// Build left hand side UI - options and buttons.
		UIBuilder uIBuilder2 = new(rectList[0].Slot);
		Slot _layoutRoot = uIBuilder2.VerticalLayout(4f, 0f, new Alignment()).Slot;
		uIBuilder2.FitContent(SizeFit.Disabled, SizeFit.MinSize);
		uIBuilder2.Style.Height = 24f;
		UIBuilder uIBuilder3 = uIBuilder2;

		// Slot reference to which changes will be applied.
		_text = "Armature slot:";
		uIBuilder3.Text(in _text);
		uIBuilder3.Next("Root");
		uIBuilder3.Current.AttachComponent<RefEditor>().Setup(ProcessRoot.Reference);
		uIBuilder3.Spacer(24f);

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
		uIBuilder3.HorizontalElementWithLabel(in _text, 0.5f, () => uIBuilder3.PrimitiveMemberEditor(useTagMode.Value));
		_text = "1 = IgnoreTag";
		uIBuilder3.Text(in _text);
		_text = " 2 = IncludeOnlyWithTag";
		uIBuilder3.Text(in _text);
		_text = "3 = ExcludeAllWithTag";
		uIBuilder3.Text(in _text);
		uIBuilder3.Spacer(24f);

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

		// Buttons for batch actions.
		_text = "List matching DynamicBoneChains";
		uIBuilder3.Button(in _text).LocalPressed += ListDynamicBones;
		_text = "Remove all DynamicBoneChains";
		uIBuilder3.Button(in _text).LocalPressed += RemoveAllDynamicBoneChains;
		_text = "Attach DynamicBoneChains";
		uIBuilder3.Button(in _text).LocalPressed += AttachDynamicBones;
		uIBuilder3.Spacer(24f);
		resultsText = uIBuilder3.Text(in _text);

        // Build right hand side UI - list of found DynamicBoneChain.
        UIBuilder uIBuilder4 = new(rectList[1].Slot);
		uIBuilder4.ScrollArea();
		uIBuilder4.VerticalLayout(10f, 4f);
		_scrollAreaRoot = uIBuilder4.FitContent(SizeFit.Disabled, SizeFit.MinSize).Slot;

		// Prepare UIBuilder for addding elements to DynamicBoneChain list.
		_listBuilder = uIBuilder4;
		_listBuilder.Style.MinHeight = 20f;

		WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
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
				PopulateList();
				_count = 0;
				Recursion(ProcessRoot.Reference.Target);
				ShowResults(_count + " DynamicBoneChains attached!");

			}
			else ShowResults("No target root slot set.");
		});
	}

	private void ListDynamicBones(IButton button, ButtonEventData eventData)
	{
		WizardSlot.RunSynchronously(() =>
		{
			if (ProcessRoot != null)
			{
				_count = 0;
				PopulateList();
				ShowResults(_count + " DynamicBoneChains found!");

			}
			else ShowResults("No target root slot set.");
		});
	}

	private void Recursion(Slot slot)
	{
		for (int i = 0; i < slot.ChildrenCount; i++)
		{
			if (NameCheck(slot[i].Name)
			&& (slot[i].GetComponent<Wiggler>() == null)
			&& !slot[i].Name.Contains("Wiggler")
			&& !slot[i].Name.Contains("Haptics")
			&& !slot[i].Name.Contains("Button") // Because it gets confused with "butt"
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
				Recursion(slot[i]);
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
			if (slotCanidateName.StartsWith("DynBone") || slotCanidateName.ToLower().Contains(item.ToLower()))
			{
				return true;
			}
		}
		return false;
	}

	private void CreateScrollListElement(DynamicBoneChain mc)
	{
		Slot _elementRoot = _listBuilder.Next("Element");
		var _refField = _elementRoot.AttachComponent<ReferenceField<DynamicBoneChain>>();
		_refField.Reference.Target = mc;
		UIBuilder _listBuilder2 = new(_elementRoot);
		_listBuilder2.NestInto(_elementRoot);
		_listBuilder2.VerticalLayout(4f, 4f);
		_listBuilder2.HorizontalLayout(10f);
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
				|| (useTagMode.Value.Value == UseTagMode["IncludeOnlyWithTag"] && mc.Slot.Tag.ToLower() == tag.TargetString.ToLower())
				|| (useTagMode.Value.Value == UseTagMode["ExcludeAllWithTag"] && mc.Slot.Tag.ToLower() != tag.TargetString.ToLower()));
			}))
			{
				_count++;
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

	private void RemoveAllDynamicBoneChains(IButton button, ButtonEventData eventData)
	{
        List<Slot> tagList = new();
		ProcessRoot.Reference.Target.GetAllChildren(tagList);

		int _count = 0;
		foreach (var slot in tagList)
		{
			DynamicBoneChain boneChain = slot.GetComponent<DynamicBoneChain>();
			if (boneChain != null)
			{
				boneChain.Enabled = false;
				boneChain.Destroy();
				slot.Tag = string.Empty; // Implies exsistence of "boneRemoved"
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
