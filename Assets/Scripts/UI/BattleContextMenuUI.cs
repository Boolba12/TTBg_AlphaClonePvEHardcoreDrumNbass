using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleContextMenuUI : MonoBehaviour
{
    [Header("Unit Count")]
    public Text unitCountLabel;
    public Button addUnitButton;
    public Button discardUnitButton;
    public int minimumUnits = 1;
    public int maximumUnits = 6;

    [Header("Weapon Selection")]
    public Button weaponButton;
    public Text weaponNameLabel;
    public Text weaponDescriptionLabel;
    public List<BattleWeaponDefinition> availableWeapons = new List<BattleWeaponDefinition>();

    [Header("State")]
    public GameObject menuRoot;
    public int startingUnitCount = 1;

    private int currentUnitCount;
    private int currentWeaponIndex = -1;

    private void Awake()
    {
        currentUnitCount = Mathf.Clamp(startingUnitCount, minimumUnits, maximumUnits);
        BattleSetupContext.SetSelection(currentUnitCount, GetCurrentWeapon());

        if (addUnitButton != null)
            addUnitButton.onClick.AddListener(AddUnit);

        if (discardUnitButton != null)
            discardUnitButton.onClick.AddListener(DiscardUnit);

        if (weaponButton != null)
            weaponButton.onClick.AddListener(CycleWeapon);

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (addUnitButton != null)
            addUnitButton.onClick.RemoveListener(AddUnit);

        if (discardUnitButton != null)
            discardUnitButton.onClick.RemoveListener(DiscardUnit);

        if (weaponButton != null)
            weaponButton.onClick.RemoveListener(CycleWeapon);
    }

    public void AddUnit()
    {
        if (currentUnitCount >= maximumUnits)
            return;

        currentUnitCount++;
        CommitSelection();
    }

    public void DiscardUnit()
    {
        if (currentUnitCount <= minimumUnits)
            return;

        currentUnitCount--;
        CommitSelection();
    }

    public void CycleWeapon()
    {
        if (availableWeapons.Count == 0)
        {
            CommitSelection();
            return;
        }

        currentWeaponIndex = (currentWeaponIndex + 1) % availableWeapons.Count;
        CommitSelection();
    }

    public void SelectWeapon(BattleWeaponDefinition weapon)
    {
        if (weapon == null)
            return;

        int nextIndex = availableWeapons.IndexOf(weapon);
        if (nextIndex < 0)
            return;

        currentWeaponIndex = nextIndex;
        CommitSelection();
    }

    public void ConfirmBattleSetup()
    {
        BattleSetupContext.Confirm();

        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void CommitSelection()
    {
        BattleSetupContext.SetSelection(currentUnitCount, GetCurrentWeapon());
        UpdateUI();
    }

    private BattleWeaponDefinition GetCurrentWeapon()
    {
        if (availableWeapons.Count == 0)
            return null;

        if (currentWeaponIndex < 0 || currentWeaponIndex >= availableWeapons.Count)
            currentWeaponIndex = 0;

        return availableWeapons[currentWeaponIndex];
    }

    private void UpdateUI()
    {
        if (unitCountLabel != null)
            unitCountLabel.text = currentUnitCount.ToString();

        BattleWeaponDefinition currentWeapon = GetCurrentWeapon();

        Image weaponButtonImage = weaponButton != null ? weaponButton.image : null;
        if (weaponButtonImage != null)
        {
            weaponButtonImage.sprite = currentWeapon != null ? currentWeapon.icon : null;
            weaponButtonImage.enabled = weaponButtonImage.sprite != null;
        }

        if (weaponNameLabel != null)
            weaponNameLabel.text = currentWeapon != null ? currentWeapon.weaponName : "No weapon";

        if (weaponDescriptionLabel != null)
            weaponDescriptionLabel.text = currentWeapon != null ? currentWeapon.description : "Assign a weapon asset to begin.";
    }
}