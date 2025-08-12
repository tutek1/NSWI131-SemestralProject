using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class NormalMapManager : MonoBehaviour
{
    [Header("Manual Testing")]
    [SerializeField] private int charIdxToUse;

    [Header("Time Testing")]
    [SerializeField] private int repeatsPerSlope = 1;
    [SerializeField] private float slopeStart = 0.0f;
    [SerializeField] private float slopeEnd = 0.4f;
    [SerializeField] private float slopeStep = 0.05f;
    [SerializeField] private string file = "test.txt";
    [SerializeField] private bool useOptimized = false;
    [SerializeField] private bool TEST = false;


    [Header("Refs")]
    [SerializeField] private Slider _edgesStrengthSlider;
    [SerializeField] private Slider _edgeBlurSlider;
    [SerializeField] private Slider _borderStrengthSlider;
    [SerializeField] private Slider _borderBlurSlider;
    [SerializeField] private Slider _borderSoftenSlider;
    [SerializeField] private Slider _borderSlopePercentageSlider;
    [SerializeField] private Slider _finalBlurSlider;

    [SerializeField] private Toggle _autoGenerateToggle;
    [SerializeField] private Toggle _showNormalMapToggle;

    [SerializeField] private FajtovPlayerAnimator _previewAnimator;
    [SerializeField] private GameObject _popUpPrefab;
    

    private NormalMapGenerator _normalMapGenerator;
    private CharacterData _selectedCharacter;
    private Vector2 _offscreenPosition;

    // Get references
    void Start()
    {
        _normalMapGenerator = GetComponent<NormalMapGenerator>();
        _offscreenPosition = transform.position;

        // TODO remove
        if (GlobalState.AllCharacters.Count == 0) CharacterLoader.LoadAllCharacters(GlobalState.AllCharacters);

        int charIdx = charIdxToUse;
        if (charIdxToUse >= GlobalState.AllCharacters.Count)
        {
            Debug.LogWarning("Wrong character index! There are only " + GlobalState.AllCharacters.Count + " characters.");
            charIdx = 0;
        }
        InitializeMenu(GlobalState.AllCharacters[charIdx]);
    }

    void Update()
    {
        if (TEST)
        {
            TEST = false;

            NormalMapSettings settings = new NormalMapSettings();
            settings.sourceTexture = _selectedCharacter.preview.texture;
            settings.strengthEdges = (int)_edgesStrengthSlider.value;
            settings.blurEdgesRadius = (int)_edgeBlurSlider.value;
            settings.strengthBorder = (int)_borderStrengthSlider.value;
            settings.blurBorderRadius = (int)_borderBlurSlider.value;
            settings.softenBorder = (int)_borderSoftenSlider.value;
            settings.finalBlurRadius = (int)_finalBlurSlider.value;

            // Set if use optimized or not
            _normalMapGenerator.useOptimized = useOptimized;

            // +1 because we use bound inclusive outer loop
            int numIter = (int)Mathf.Round((slopeEnd - slopeStart) / slopeStep + 1f) * repeatsPerSlope;
            double[] times = new double[numIter];
            int timesIdx = 0;

            // Measure all times and save them in array
            for (float currSlope = slopeStart; currSlope <= slopeEnd + 0.001f; currSlope += slopeStep)
            {
                settings.slopePercentageBorder = currSlope;

                for (int repeat = 0; repeat < repeatsPerSlope; repeat++)
                {
                    double startTime = Time.realtimeSinceStartupAsDouble;
                    _normalMapGenerator.GenerateNormalMap(settings);
                    double endTime = Time.realtimeSinceStartupAsDouble;
                    times[timesIdx] = (endTime - startTime) * 1000; // to ms
                    timesIdx++;
                }

            }

            // Save all times to file
            string fullPath = Path.Combine(Application.dataPath, file);
            using (StreamWriter writer = new StreamWriter(fullPath, false))
            {
                timesIdx = 0;
                for (float currSlope = slopeStart; currSlope <= slopeEnd + 0.001f; currSlope += slopeStep)
                {
                    for (int repeat = 0; repeat < repeatsPerSlope; repeat++)
                    {
                        writer.WriteLine(currSlope.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        + ","
                        + times[timesIdx].ToString("F3", System.Globalization.CultureInfo.InvariantCulture));

                        timesIdx++;
                    }
                }
            }

            Debug.Log("TESTED");
        }
    }


    // Initializes the normal map menu with the selected character
    public void InitializeMenu(CharacterData selectedCharacter)
    {
        _selectedCharacter = selectedCharacter;
        _previewAnimator.Initialize(_selectedCharacter);
        _previewAnimator.ShowPreviewIcon();

        // Update the normal toggle
        OnShowNormalMapToggle();
    }


    // Callback for the showNormalMap toggle button
    public void OnShowNormalMapToggle()
    {
        if (_showNormalMapToggle.isOn) 
        {
            if (_selectedCharacter.previewNormalMap == null) OnGeneratePreviewButtonDown();
            else _previewAnimator.ShowPreviewIcon(previewNormal: true);
        }
        else
        {
            _previewAnimator.ShowPreviewIcon(previewNormal: false);
        }
    }


    // Callback for any parameter change
    public void OnSliderChange()
    {
        if (_autoGenerateToggle.isOn) OnGeneratePreviewButtonDown();
    }


    // Callback for generate preview button
    public void OnGeneratePreviewButtonDown()
    {
        NormalMapSettings settings = new NormalMapSettings();
        settings.sourceTexture = _selectedCharacter.preview.texture;
        settings.strengthEdges = (int)_edgesStrengthSlider.value;
        settings.blurEdgesRadius = (int)_edgeBlurSlider.value;
        settings.strengthBorder = (int)_borderStrengthSlider.value;
        settings.blurBorderRadius = (int)_borderBlurSlider.value;
        settings.softenBorder = (int)_borderSoftenSlider.value;
        settings.slopePercentageBorder = _borderSlopePercentageSlider.value;
        settings.finalBlurRadius = (int)_finalBlurSlider.value;


        Texture2D texture = _normalMapGenerator.GenerateNormalMap(settings);
        _selectedCharacter.previewNormalMap = texture;                                     
        OnShowNormalMapToggle();
    }


    // Saves and generates all normal maps for a character
    public void OnGenerateAndSaveButtonDown()
    {
        // TODO Use this for async generation 
        //PopUpWindow popUpWindow = Instantiate(_popUpPrefab).GetComponent<PopUpWindow>();
        //popUpWindow.Initialize("Generating all normal maps...");

        // Generate all normal maps for all animations
        var animEnumerator = _selectedCharacter.GetAnimationEnumerator();
        while(animEnumerator.MoveNext())
        {
            GenerateNormalMapsForAnimation(animEnumerator.Current);
        }

        // Show the normal map
        OnGeneratePreviewButtonDown();

        int charIdx = GlobalState.AllCharacters.IndexOf(_selectedCharacter);

        // TODO test if next line needed
        GlobalState.AllCharacters[charIdx] = _selectedCharacter;

        CharacterLoader.SaveCharacterNormalMaps(_selectedCharacter);

        PopUpWindow popUpWindow = Instantiate(_popUpPrefab).GetComponent<PopUpWindow>();
        popUpWindow.Initialize("Normal maps generated!");
    }


    private void GenerateNormalMapsForAnimation(FajtovAnimationClip anim)
    {
        NormalMapSettings settings = new NormalMapSettings();
        settings.sourceTexture = _selectedCharacter.preview.texture;
        settings.strengthEdges = (int)_edgesStrengthSlider.value;
        settings.blurEdgesRadius = (int)_edgeBlurSlider.value;
        settings.strengthBorder = (int)_borderStrengthSlider.value;
        settings.blurBorderRadius = (int)_borderBlurSlider.value;
        settings.softenBorder = (int)_borderSoftenSlider.value;
        settings.slopePercentageBorder = _borderSlopePercentageSlider.value;
        settings.finalBlurRadius = (int)_finalBlurSlider.value;

        List<Texture2D> normalMaps = new List<Texture2D>();

        foreach (Sprite sprite in anim.frames)
        {
            settings.sourceTexture = sprite.texture;
            Texture2D texture = _normalMapGenerator.GenerateNormalMap(settings);
            
            normalMaps.Add(texture);                                                        
        }

        anim.normalMapframes = normalMaps.ToArray();
    }


    public void OnDeleteButtonDown()
    {
        _selectedCharacter.attackAnim.normalMapframes = null;
        _selectedCharacter.hurtAnim.normalMapframes = null;
        _selectedCharacter.idleAnim.normalMapframes = null;
        _selectedCharacter.jumpAnim.normalMapframes = null;
        _selectedCharacter.walkAnim.normalMapframes = null;
        _selectedCharacter.blockAnim.normalMapframes = null;
        _selectedCharacter.deathAnim.normalMapframes = null;

        CharacterLoader.DeleteCharacterNormalMaps(_selectedCharacter);

        PopUpWindow popUpWindow = Instantiate(_popUpPrefab).GetComponent<PopUpWindow>();
        popUpWindow.Initialize("Normal maps deleted!");
    }

    public void OnBackButtonDown()
    {
        // TODO go back
        // TODO reload normal maps on current character
    }
}
