using TMPro;
using UnityEditor.Search;
using UnityEngine;

public class BuildingController : MonoBehaviour
{

    [Header("Building Settings")]
    public GameObject buildingPrefab;
    public GameObject ground;
    public GameObject buildingPreviewPrefab;
    public TextMeshProUGUI buildingInfoText;

    [Header("Building Properties")]
    public bool isBuildingActive = false;

    private GameObject previewBuildingInstance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("Ground object not found. Please ensure there is a GameObject named 'Ground' in the scene.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildingMode();
        }

        if (isBuildingActive && Input.GetKeyDown(KeyCode.Mouse0))
        {
            PlaceBuilding();
        }

        if (isBuildingActive)
        {
            UpdatePreview();
        }
    }

    void ToggleBuildingMode()
    {
        isBuildingActive = !isBuildingActive;

        if (isBuildingActive)
        {
            ActivateBuildingMode();
        }
        else
        {
            DeactivateBuildingMode();
        }
    }

    void ActivateBuildingMode()
    {
        // Logic to activate building mode, e.g., show building UI, enable building placement
        Debug.Log("Building mode activated.");
        if (buildingInfoText != null)
        {
            buildingInfoText.enabled = true;
        }
        else
        {
            Debug.LogWarning("Building info text UI element is not assigned.");
        }
        // instantiate a preview building
        if (buildingPreviewPrefab != null)
        {
            previewBuildingInstance = Instantiate(buildingPreviewPrefab, GetMouseWorldPositionOnGround(), Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Building preview prefab is not assigned.");
        }
    }

    void DeactivateBuildingMode()
    {
        // Logic to deactivate building mode, e.g., hide building UI, disable building placement
        Debug.Log("Building mode deactivated.");
        if (buildingInfoText != null)
        {
            buildingInfoText.enabled = false;
        }
        else
        {
            Debug.LogWarning("Building info text UI element is not assigned.");
        }

        // Destroy the preview building if it exists
        if (previewBuildingInstance != null)
        {
            Destroy(previewBuildingInstance);
            previewBuildingInstance = null;
        }
    }

    void PlaceBuilding()
    {
        // Logic to place the building in the game world
        Vector3 position = GetMouseWorldPositionOnGround();
        Instantiate(buildingPrefab, position, Quaternion.identity);
        Debug.Log("Building placed at: " + position);

        // Deactivate building mode after placing the building
        DeactivateBuildingMode();
        isBuildingActive = false;
    }

    Vector3 GetMouseWorldPositionOnGround()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            return hit.point + Vector3.up * 1.0f; // Adjust height slightly above the ground
        }

        // If the raycast doesn't hit anything, return a default position
        return ground.transform.position;
    }

    void UpdatePreview()
    {
        if (previewBuildingInstance != null)
        {
            Vector3 position = GetMouseWorldPositionOnGround();
            previewBuildingInstance.transform.position = position;
        }
    }
}
