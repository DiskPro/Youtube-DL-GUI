﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace youtube_dl
{
    public partial class AboutForm : Form
    {
        private int clicks = 0;

        public AboutForm()
        {
            InitializeComponent();
        }

        private void AboutForm_Load(object sender, EventArgs e)
        {
            VersionLabel.Text = Application.ProductVersion;
        }

        private void title_Click(object sender, EventArgs e)
        {
            if(clicks > 7)
            {
                System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=taQvvuQD8DM");
                clicks = 0;
            }
            else
            {
                clicks++;
            }
            
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://diskpro.github.io/Youtube-DL-GUI/");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://guilhermefrancisco.net/");
        }
    }
}
