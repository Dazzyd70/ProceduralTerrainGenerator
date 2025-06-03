using UnityEngine;
using UnityEngine.UI;

// Handles switching between overview and player camera views,
// toggling player controls, and hiding the UI panel when in first-person mode.

public class UICameraSwitcher : MonoBehaviour
{
    public Camera overviewCamera;   
    public Toggle playerViewToggle;   
    public GameObject sidePanelUI;
    [SerializeField] private GameObject sidePanel;

    private Camera playerCamera;
    private MonoBehaviour playerController;
    private bool isPlayerView = false;

    void Start()
    {
        playerViewToggle.onValueChanged.AddListener(OnToggleChanged);
        playerViewToggle.isOn = false;
    }

    void Update()
    {
        // Press Tab to switch between overview and player cameras
        if (Input.GetKeyDown(KeyCode.Tab))
            playerViewToggle.isOn = !playerViewToggle.isOn;
        sidePanel.SetActive(!isPlayerView);
    }

    public void SetPlayerCamera(Camera cam)
    {
        playerCamera = cam;
        playerController = cam.GetComponentInParent<SimplePlayerController>() as MonoBehaviour;
        OnToggleChanged(playerViewToggle.isOn);
    }

    private void OnToggleChanged(bool toPlayerView)
    {
        isPlayerView = toPlayerView;

        // Enable only one camera at a time
        overviewCamera.enabled = !toPlayerView;
        if (playerCamera != null)
            playerCamera.enabled = toPlayerView;

        // Enable/disable player movement script
        if (playerController != null)
            playerController.enabled = toPlayerView;

        // Show UI only in overview mode
        sidePanelUI.SetActive(!toPlayerView);

        // Lock/unlock cursor
        Cursor.lockState = toPlayerView
            ? CursorLockMode.Locked
            : CursorLockMode.None;
        Cursor.visible = !toPlayerView;
    }
}
