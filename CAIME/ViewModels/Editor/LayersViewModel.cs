using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CAIME
{
    public class LayersViewModel : BaseViewModel
    {
        private Dictionary<LayerType, Layer> LayersMap;
        
        public ObservableCollection<Layer> Layers { get; private set; }

        public LayerType ActiveLayer        { get; private set; }
        public LayerType TopVisibleLayer    { get; private set; }

        public LayersViewModel()
        {
        }

        public bool Initialise(GameTemplate game)
        {
            LayersMap = new Dictionary<LayerType, Layer>
            {
                [LayerType.Impassable]  = new Layer(LayerType.Impassable),
                [LayerType.Roads]       = new Layer(LayerType.Roads),
                [LayerType.TownSlots]   = new Layer(LayerType.TownSlots),
                [LayerType.TownSprawl]  = new Layer(LayerType.TownSprawl),
                [LayerType.Bridges]     = new Layer(LayerType.Bridges),
                [LayerType.Rivers]      = new Layer(LayerType.Rivers),
                [LayerType.Beaches]     = new Layer(LayerType.Beaches),
                [LayerType.Regions]     = new Layer(LayerType.Regions),
                [LayerType.Attritions]  = new Layer(LayerType.Attritions),
                [LayerType.Climates]    = new Layer(LayerType.Climates),
                [LayerType.GroundTypes] = new Layer(LayerType.GroundTypes)
            };

            Layers = new ObservableCollection<Layer>
            {
                LayersMap[LayerType.Impassable],
                LayersMap[LayerType.Roads],
                LayersMap[LayerType.TownSlots],
                LayersMap[LayerType.TownSprawl],
                LayersMap[LayerType.Bridges],
                LayersMap[LayerType.Rivers],
                LayersMap[LayerType.Beaches],
                LayersMap[LayerType.Regions],
                LayersMap[LayerType.Attritions],
                LayersMap[LayerType.Climates],
                LayersMap[LayerType.GroundTypes]
            };

            if (game == GameTemplate.Rome2 ||
                game == GameTemplate.Attila ||
                game == GameTemplate.Thrones_Of_Britannia ||
                game == GameTemplate.Three_Kingdoms)
            {
                LayersMap[LayerType.TradeRoutes] = new Layer(LayerType.TradeRoutes);
                Layers.Insert(1, LayersMap[LayerType.TradeRoutes]);
            }

            if (game == GameTemplate.Attila ||
                game == GameTemplate.Thrones_Of_Britannia ||
                game == GameTemplate.Warhammer ||
                game == GameTemplate.Warhammer2 ||
                game == GameTemplate.Warhammer3 ||
                game == GameTemplate.Three_Kingdoms ||
                game == GameTemplate.Troy ||
                game == GameTemplate.Pharaoh ||
                game == GameTemplate.Pharaoh_Dynasties)
            {
                LayersMap[LayerType.RegionBorders] = new Layer(LayerType.RegionBorders);
                Layers.Insert(Layers.Count - 4, LayersMap[LayerType.RegionBorders]);

                LayersMap[LayerType.Restrictions] = new Layer(LayerType.Restrictions);
                Layers.Insert(Layers.Count - 5, LayersMap[LayerType.Restrictions]);
            }

            if (game == GameTemplate.Warhammer3 ||
                game == GameTemplate.Three_Kingdoms)
            {
                LayersMap[LayerType.AreasOfInterest] = new Layer(LayerType.AreasOfInterest);
                Layers.Insert(Layers.Count - 4, LayersMap[LayerType.AreasOfInterest]);
            }

            OnPropertyChanged(nameof(Layers));

            LayersMap[LayerType.GroundTypes].SetActive(isActive: true, raiseEvent: true);
            LayersMap[LayerType.GroundTypes].SetVisible(isVisible: true, raiseEvent: true);

            return true;
        }

        /// <summary>
        /// Updates <see cref="ActiveLayer"/>
        /// </summary>
        public void UpdateActiveLayer(LayerType layer)
        {
            ActiveLayer = layer;
        }

        /// <summary>
        /// Updates <see cref="TopVisibleLayer"/>
        /// </summary>
        public void UpdateTopLayer()
        {
            for (int index = 0; index < Layers.Count; ++index)
            {
                if (Layers[index].IsVisible)
                {
                    TopVisibleLayer = Layers[index].Type;
                    return;
                }
            }

            TopVisibleLayer = LayerType.GroundTypes;
        }

        /// <summary>
        /// Set provided layer to be active (raises <see cref="Layer.ActiveLayerChanged"/> event)
        /// </summary>
        public void SetActiveLayer(LayerType layer)
        {
            ActiveLayer = layer;
            LayersMap[ActiveLayer].SetActive(true, raiseEvent: true);
        }

        /// <summary>
        /// Set provided layer to be visible (raises <see cref="Layer.VisibilityChanged"/> event)
        /// </summary>
        public void SetVisibleLayer(LayerType layer)
        {
            LayersMap[layer].SetVisible(true, raiseEvent: true);
        }

        public void SetColours(ColourTable colourTable)
        {
            colourTable.SetColours(Layers);
        }

        /// <summary>
        /// Get active layer (can be only one)
        /// </summary>
        public Layer GetActiveLayer()
        {
            return LayersMap[ActiveLayer];
        }

        /// <summary>
        /// Get topmost visible layer
        /// </summary>
        /// <returns>First visible layer in layers stack</returns>
        public Layer GetTopLayer()
        {
            return LayersMap[TopVisibleLayer];
        }

        /// <summary>
        /// Determines whether newly painted colour can be displayed
        /// </summary>
        public bool CanDisplay(int colourIndex)
        {
            return CanDisplay(GetActiveLayer(), colourIndex);
        }

        /// <summary>
        /// Determines whether a colour painted on the given layer can be displayed
        /// </summary>
        public bool CanDisplay(Layer layer, int colourIndex)
        {
            foreach (var topLayer in Layers)
            {
                if (topLayer.IsVisible)
                {
                    if (topLayer.Colours[colourIndex] != ColourTable.Zero && (int)topLayer.Type < (int)layer.Type)
                    {
                        return false;
                    }
                    else
                    if (topLayer.Type == layer.Type)
                    {
                        break;
                    }
                }
            }

            return true;
        }

        public Layer GetLayerByName(string name)
        {
            foreach (var layer in Layers)
            {
                if (layer.Name == name)
                {
                    return layer;
                }
            }

            return null;
        }

        public List<LayerType> GetLayers()
        {
            var list = new List<LayerType>(Layers.Count);

            foreach (var layer in Layers)
            {
                list.Add(layer.Type);
            }

            return list;
        }

        public Layer GetLayer(LayerType type)
        {
            return LayersMap[type];
        }
    }
}
