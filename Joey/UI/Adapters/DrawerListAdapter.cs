﻿using System;
using System.Collections.Generic;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.UI.Adapters
{
    class DrawerListAdapter : BaseAdapter
    {
        protected static readonly int ViewTypeDrawerItem = 0;
        public static readonly int TimerPageId = 0;
        public static readonly int ReportsPageId = 1;
        public static readonly int SettingsPageId = 2;
        public static readonly int LogoutPageId = 3;
        public static readonly int FeedbackPageId = 4;
        private List<DrawerItem> rowItems;
        private readonly AuthManager authManager;

        public DrawerListAdapter ()
        {
            rowItems = new List<DrawerItem> () {
                new DrawerItem () {
                    Id = TimerPageId,
                    TextResId = Resource.String.MainDrawerTimer,
                    ImageResId = Resource.Drawable.IcNavTimer,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = ReportsPageId,
                    TextResId = Resource.String.MainDrawerReports,
                    ImageResId = Resource.Drawable.IcNavReports,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = SettingsPageId,
                    TextResId = Resource.String.MainDrawerSettings,
                    ImageResId = Resource.Drawable.IcNavSettings,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = FeedbackPageId,
                    TextResId = Resource.String.MainDrawerFeedback,
                    ImageResId = Resource.Drawable.IcNavFeedback,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = LogoutPageId,
                    TextResId = Resource.String.MainDrawerLogout,
                    ImageResId = Resource.Drawable.IcNavLogout,
                    IsEnabled = true,
                }
            };
            authManager = ServiceContainer.Resolve<AuthManager> ();
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView;

            if (view == null) {
                view = LayoutInflater.FromContext (parent.Context).Inflate (
                           Resource.Layout.MainDrawerListItem, parent, false);
                view.Tag = new DrawerItemViewHolder (view);
            }

            var holder = (DrawerItemViewHolder)view.Tag;
            holder.Bind (GetDrawerItem (position));
            return view;
        }

        public override int Count
        {
            get { return rowItems.Count; }
        }

        public override Java.Lang.Object GetItem (int position)
        {
            return null;
        }

        public int GetItemPosition (int id)
        {
            var idx = rowItems.FindIndex (i => i.Id == id);
            return idx >= 0 ? idx + 1 : -1;
        }

        private DrawerItem GetDrawerItem (int position)
        {
            return rowItems [position];
        }

        public override long GetItemId (int position)
        {
            return GetDrawerItem (position).Id;
        }

        public override bool IsEnabled (int position)
        {
            var item = GetDrawerItem (position);
            return item != null && item.IsEnabled;
        }

        private class DrawerItem
        {
            public int Id;
            public int TextResId;
            public int ImageResId;
            public int ChildOf = 0;
            public bool IsEnabled;
        }

        private class DrawerItemViewHolder : BindableViewHolder<DrawerItem>
        {
            public ImageView IconImageView { get; private set; }

            public TextView TitleTextView { get; private set; }

            public DrawerItemViewHolder (View root) : base (root)
            {
                IconImageView = root.FindViewById<ImageView> (Resource.Id.IconImageView);
                TitleTextView = root.FindViewById<TextView> (Resource.Id.TitleTextView).SetFont (Font.RobotoLight);
            }

            protected override void Rebind ()
            {
                if (DataSource == null) {
                    return;
                }

                IconImageView.SetImageResource (DataSource.ImageResId);
                TitleTextView.SetText (DataSource.TextResId);
                TitleTextView.Enabled = DataSource.IsEnabled;
            }
        }
    }
}

