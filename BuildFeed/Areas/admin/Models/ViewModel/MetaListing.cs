﻿using System.Collections.Generic;
using System.Linq;
using BuildFeed.Model;

namespace BuildFeed.Areas.admin.Models.ViewModel
{
    public class MetaListing
    {
        public IEnumerable<IGrouping<MetaType, MetaItemModel>> CurrentItems { get; set; }
        public IEnumerable<IGrouping<MetaType, MetaItemModel>> NewItems { get; set; }
    }
}