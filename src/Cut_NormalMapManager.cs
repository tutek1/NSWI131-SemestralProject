using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

//
//  This file is has some cut functions and variables that are not important
//  to the time measuring. The full version of the file, shaders,
//  and other files can be found in the folder `FullFiles`.
//

public class NormalMapManager : MonoBehaviour
{
    [Header("Time Testing")]
    [SerializeField] private int repeatsPerSlope = 1;
    [SerializeField] private float slopeStart = 0.0f;
    [SerializeField] private float slopeEnd = 0.4f;
    [SerializeField] private float slopeStep = 0.05f;
    [SerializeField] private string file = "test.txt";
    [SerializeField] private bool useOptimized = false;
    [SerializeField] private bool TEST = false;

    private NormalMapGenerator _normalMapGenerator;

    // Run every frame
    void Update()
    {
        // Bool variable that can be pressed in the UI
        if (TEST)
        {
            TEST = false;

            // Get settings from UI elements
            NormalMapSettings settings = new NormalMapSettings();
            settings.sourceTexture = _selectedCharacter.preview.texture;
            settings.strengthEdges = (int)_edgesStrengthSlider.value;
            settings.blurEdgesRadius = (int)_edgeBlurSlider.value;
            settings.strengthBorder = (int)_borderStrengthSlider.value;
            settings.blurBorderRadius = (int)_borderBlurSlider.value;
            settings.softenBorder = (int)_borderSoftenSlider.value;
            settings.finalBlurRadius = (int)_finalBlurSlider.value;

            // Set if use the optimized version of code base or not
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
}
