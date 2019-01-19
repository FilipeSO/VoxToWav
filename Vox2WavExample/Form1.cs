﻿using AudioFormatLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ANOPER
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            string strAudioFilePath = Directory.GetFiles(Environment.CurrentDirectory, "VOXAudioSample.vox")[0];
            Vox2Wav.Decode(strAudioFilePath, out string wavFilePath);
            WavFileFormat.Normalize(wavFilePath, out string normWavFilePath);
            SoundPlayer simpleSound = new SoundPlayer(normWavFilePath);
            simpleSound.Play();
        }


    }
}
