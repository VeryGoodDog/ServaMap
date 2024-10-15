// Thank you Drakker for pretty much everything here.
// I could only figure out what to get rid of.

const dataFolder = '/data';

// Get the data we need for the URL copy/paste handling
const title = document.title;
var adr = document.location.href.replace(/\?.*/, "");

var worldOriginOffset = null;
fetch(dataFolder + '/worldOriginOffset.json')
    .then(res => res.json())
    .then(json => {
        worldOriginOffset = json.worldOriginOffset;
        console.log(worldOriginOffset);
    });

const args = new URLSearchParams(document.location.href.replace(/.*\?/, ''));

var cx = 0;
var cy = 0;
var zm = 6;

// if (args.has('x')) {
//     cx = args.get('x')
// }
// if (args.has('y')) {
//     cy = args.get('y')
// }
// if (args.has('zoom')) {
//     zm = args.get('zoom')
// }

// Find the inspector
const inspector = document.getElementById('status');

// Path for all the icons, layers with multiple icons use a dict
const icons = {
    'Traders': 'icons/trader.svg',
    'Translocators': 'icons/spiral.svg',
    'Landmarks': {
        'Base': 'icons/home.svg',
        'Misc': 'icons/star1.svg'
    }
}

// Icons color references table by icon type
const colorsRef = {
    'Traders': {
        'artisan': [0, 240, 240],
        'buildmaterials': [255, 0, 0],
        'clothing': [0, 128, 0],
        'commodities': [128, 128, 128],
        'agriculture': [200, 192, 128],
        'foods': [200, 192, 128],
        'furniture': [255, 128, 0],
        'luxuries': [0, 0, 255],
        'survivalgoods': [255, 255, 0],
        'treasurehunter': [160, 0, 160],
        'unknown': [48, 48, 48]
    },
    'Translocators': {
        'Translocator': [192, 0, 192],
        'Named Translocator': [192, 128, 255],
        'Spawn Translocator': [0, 192, 192]
    },
    'Landmarks': {
        'Base': [192, 192, 192],
        'Misc': [224, 224, 224]
    }
}

const toolsRef = {
    'zoomIn': {
        'id': 'zoomIn',
        'icon': '+',
        'title': 'Zoom in',
        'callback': function (event) {
            view.animate({zoom: view.getConstrainedZoom(view.getZoom() + 1), duration: 100});
        }
    },
    'zoomOut': {
        'id': 'zoomOut',
        'icon': '−',
        'title': 'Zoom out',
        'callback': function (event) {
            view.animate({zoom: view.getConstrainedZoom(view.getZoom() - 1), duration: 100});
        }
    },
    'origin': {
        'id': 'origin',
        'icon': '🧭',
        'title': 'Move and zoom to the server spawn (H)',
        'callback': function (event) {
            goToCoords('0,0');
        }
    },
    'goToCoords': {
        'id': 'goToCoords',
        'icon': '🔍',
        'title': 'Move and zoom to coordinates (G)',
        'callback': function (event) {
            poper.createPopup('gps');
        }
    },
    'goToLandmark': {
        'id': 'goToLandmarks',
        'icon': '🏛',
        'title': 'Move and zoom to selected landmark (L)',
        'callback': function (event) {
            poper.createPopup('landmarks');
        }
    }
}

const popupsRef = {
    'gps': {
        'title': 'Move to coordinates',
        'css': ['c', 'gps'],
        'elements': {
            'description': {
                'type': 'p',
                'content': "Enter the coordinates you want to reach in either of these two formats:"
            },
            'description2': {
                'type': 'p',
                'content': "X,Z in game coordinates: 2050,6900"
            },
            'description3': {
                'type': 'p',
                'content': "Campaign cartographer: X = -1170, Y = 113, Z = -3800"
            },
            'input': {
                'type': 'input',
                'input-type': 'text',
                'id': 'input_data',
                'name': 'input_data',
                'label': 'Coordinates:',
                'focus': true,
                'onkeypress': "if (event.keyCode == 13) { if (goToCoords(document.getElementById('input_data').value.trim())) { poper.destroyPopup('gps'); } }"
            }
        },
        'controls': {
            'Ok': {
                'title': 'Go to coordinates',
                'default': true,
                'callback': function (event) {
                    if (goToCoords(document.getElementById('input_data').value.trim())) {
                        poper.destroyPopup('gps');
                    }
                }
            },
            'Cancel': {
                'title': 'Cancel',
                'callback': function (event) {
                    poper.destroyPopup('gps');
                }
            }
        }
    },
    'landmarks': {
        'title': 'Go to landmark',
        'css': ['c', 'gps'],
        'elements': {
            'description': {
                'type': 'p',
                'content': "Select a landmark from the list."
            },
            'input': {
                'type': 'select',
                'id': 'select_data',
                'name': 'select_data',
                'label': 'Landmarks from the "Landmarks" layer:',
                'focus': true,
                'source': 'Landmarks',
                'onkeypress': "if (event.keyCode == 13) { if (goToCoords(document.getElementById('select_data').value.trim())) { poper.destroyPopup('landmarks'); } }"
            }
        },
        'controls': {
            'Ok': {
                'title': 'Go to landmark',
                'default': true,
                'callback': function (event) {
                    if (goToCoords(document.getElementById('select_data').value.trim())) {
                        poper.destroyPopup('landmarks');
                    }
                }
            },
            'Cancel': {
                'title': 'Cancel',
                'callback': function (event) {
                    poper.destroyPopup('landmarks');
                }
            }
        }
    },
    'translocator': {
        'title': 'Translocators pair information',
        'css': ['c', 'gps'],
        'elements': 'params',
        'controls': {
            'Close': {
                'title': 'Close',
                'callback': function (event) {
                    poper.destroyPopup('translocator');
                }
            }
        }
    },
    'trader': {
        'title': 'Trader information',
        'css': ['c', 'gps'],
        'elements': 'params',
        'controls': {
            'Close': {
                'title': 'Close',
                'callback': function (event) {
                    poper.destroyPopup('trader');
                }
            }
        }
    }
}

/* ######################### Popups Manager ######################### */
class PopupManager {
    constructor() {
        this.popups = {}
    }

    createPopup(which, show = true, params = false) {
        if (!(which in this.popups) && which in popupsRef) {
            var focus = false;
            var popupBlock = document.createElement('DIV');
            popupBlock.id = 'popup_' + which;
            popupBlock.className = 'popup ' + popupsRef[which]['css'].join(' ');
            var title = document.createElement('div');
            title.className = 'title';
            var txt = document.createTextNode(popupsRef[which]['title']);
            title.append(txt);
            this.popups[which] = popupBlock;
            popupBlock.append(title);
            var ref = popupsRef[which]['elements'];
            if (popupsRef[which]['elements'] === 'params') {
                ref = params['elements']
            }
            for (var el in ref) {
                var e = ref[el];
                switch (e['type']) {
                    // Paragraph //
                    case 'p':
                        var paragraph = document.createElement('P');
                        paragraph.innerHTML = e['content'];
                        popupBlock.append(paragraph);
                        break;
                    // Input box //
                    case 'input':
                        if (e['label']) {
                            var label = document.createElement('LABEL');
                            label.for = e['name'];
                            label.innerText = e['label'];
                            popupBlock.append(label);
                        }
                        var input = document.createElement('INPUT');
                        input.type = e['input-type'];
                        if (input.type === 'number') {
                            input.min = e['min'];
                            input.max = e['max'];
                            if (typeof e['default'] === 'function') {
                                input.value = e['default']();
                            } else {
                                input.value = e['default'];
                            }
                        }
                        input.name = e['name'];
                        input.id = e['id'];
                        if (e['onkeypress']) {
                            input.setAttribute('onkeypress', e['onkeypress']);
                        }
                        popupBlock.append(input);
                        if (e['focus']) {
                            focus = e['id'];
                        }
                        break;
                    // Select box //
                    case 'select':
                        if (e['label']) {
                            var label = document.createElement('LABEL');
                            label.for = e['name'];
                            label.innerText = e['label'];
                            popupBlock.append(label);
                        }
                        var input = document.createElement('SELECT');
                        input.name = e['name'];
                        input.id = e['id'];
                        let index = 0;
                        if (typeof (e['source']) === 'object') {
                            for (let option in e['source']) {
                                let op = document.createElement('OPTION');
                                op.value = e['source'][option]['val'];
                                op.innerHTML = e['source'][option]['op'];
                                input.append(op);
                                if (e['source'][option]['val'] === localStorage.theme) {
                                    input.selectedIndex = option;
                                }
                            }
                        }
                        // TODO : move that to the declaration side
                        else if (e['source'] === 'Landmarks') {
                            map.getLayers().forEach((layer) => {
                                if (layer.get('name') === e['source']) {
                                    layer.getSource().forEachFeature((feature) => {
                                        let op = document.createElement('OPTION');
                                        let coords = feature.getGeometry().getCoordinates();
                                        coords[1] *= -1;
                                        op.value = coords.toString();
                                        op.innerHTML = feature.get('label');
                                        input.append(op);
                                    });
                                }
                            });
                            Array.from(input.children).sort((a, b) => a.textContent.toUpperCase() >= b.textContent.toUpperCase()).forEach((option) => input.appendChild(option));
                            input.selectedIndex = 0;
                        } else {
                            console.log('Error: parameter must be a dict with {op, val}.');
                        }
                        if (e['onkeypress']) {
                            input.setAttribute('onkeypress', e['onkeypress']);
                        }
                        popupBlock.append(input);
                        if (e['focus']) {
                            focus = e['id'];
                        }
                        break;
                }
            }
            var controls = document.createElement('DIV');
            controls.className = 'controls';
            for (var el in popupsRef[which]['controls']) {
                var e = popupsRef[which]['controls'][el];
                var button = document.createElement('button');
                button.innerHTML = e['title'];
                button.className = 'c';
                if (e['default']) {
                    button.classList.add('default');
                }
                button.title = e['title'];
                button.id = 'popup_' + which + '_' + e['title'];
                button.onclick = e['callback'];
                controls.append(button);
            }
            popupBlock.append(controls);
            if (show) {
                this.showPopup(which, focus)
            }
        } else {
            console.log(`Can't create popup '${which}' because it already exists or its definition doesn't exist.`)
        }
    }

    showPopup(which, focus = false) {
        this.popups[which].classList.add('vis');
        var popupBG = document.createElement('DIV');
        popupBG.id = 'popupBG';
        document.body.appendChild(popupBG);
        document.body.appendChild(this.popups[which]);
        if (focus) {
            document.getElementById(focus).focus();
        }
    }

    destroyPopup(which) {
        this.popups[which].remove();
        delete this.popups[which];
        document.getElementById('popupBG').remove();
    }
}

/* ######################### Tools ######################### */
class Tools {
    constructor() {
        this.tools = {}
    }

    addTools() {
        var toolsBlock = document.getElementById('tools');
        var btHide = document.createElement('button');
        for (var el in toolsRef) {
            var e = toolsRef[el];
            var button = document.createElement('button');
            button.innerHTML = e['icon'];
            button.title = e['title'];
            button.id = e['id'];
            button.onclick = e['callback'];
            toolsBlock.append(button)
        }
    }

    enableTool(which) {
        console.log('not yet');
    }

    disableTool(which) {
        console.log('not yet dis');
    }
}

/* ######################### View movement function ######################### */
function goToCoords(where) {
    where = where.replace(/[\s\u00A0\t]/g, '');
    if (where.match(/,/g) && where.match(/,/g).length > 0) {
        where = where.replace(/[^\d,-]/g, '');
        var xy = where.split(/,/);
        xy[1] = -xy[1]
        if (map.getView().getZoom() < 8) {
            view.setZoom(9);
        }
        //if (xy.length == 2) { view.setCenter([xy[0], xy[1]]); }
        //else { view.setCenter([xy[0], xy[2]]); }
        if (xy.length === 2) {
            view.animate({'center': [xy[0], xy[1]], 'duration': 200});
        } else {
            view.animate({'center': [xy[0], xy[2]], 'duration': 200});
        }
        return true
    } else if (where.match(/=/g) && where.match(/=/g).length === 3) {
        where = where.replace(/[^\d=-]/g, '').replace(/=/, '');
        var xyz = where.split(/=/);
        xyz[2] = -xyz[2];
        if (map.getView().getZoom() < 8) {
            view.setZoom(9);
        }
        view.animate({'center': [xyz[0], xyz[2]], 'duration': 200});
        return true
    }
    return false
}

function absoluteToRelative(abs) {
    return [abs[0] - worldOriginOffset[0] + 31, -abs[1] - worldOriginOffset[2] + 31];
}

/* ######################### LayerSwitcher ######################### */
class LayerSwitcher {
    constructor(elementId) {
        this.el = document.getElementById(elementId);
        this.layers = {}
    }

    toggleVis(layerName) {
        map.getLayers().forEach(function (layer) {
            if (layer.get('name') === layerName) {
                if (layer.get('visible')) {
                    layer.setVisible(false);
                    document.getElementById('layerSwitcherBtHide' + layer.get('name')).innerHTML = '👁';
                    switcher.toggleLegendVis(layer.get('name'), true);
                } else {
                    layer.setVisible(true);
                    document.getElementById('layerSwitcherBtHide' + layer.get('name')).innerHTML = '◌';
                }
            }
        })
    }

    toggleLegendVis(legendName, hideOnly) {
        var legend = document.getElementById('legend' + legendName);
        if (legend.showLegend === true) {
            legend.showLegend = false;
            legend.style.display = 'none';
            document.getElementById('layerSwitcherBtLegend' + legendName).innerHTML = '▽';
        } else if (!hideOnly) {
            legend.showLegend = true;
            legend.style.display = '';
            document.getElementById('layerSwitcherBtLegend' + legendName).innerHTML = '△';
        }
    }

    buildLegend(layer) {
        var layerBlock = document.createElement('DIV');
        layerBlock.id = 'ls' + layer.get('name');
        layerBlock.className = 'layerBlock';
        this.el.append(layerBlock);
        var title = document.createElement('div');
        title.className = 'layerSwitcherTitle';
        var txt = document.createTextNode(layer.get('name'));
        title.append(txt);
        var btHide = document.createElement('button');
        btHide.layerSwitch = layer.get('name');
        btHide.innerHTML = '◌';
        btHide.className = 'c';
        btHide.title = 'Toggle the visibility for the ' + layer.get('name') + ' layer.';
        btHide.id = 'layerSwitcherBtHide' + layer.get('name');
        btHide.onclick = function (e) {
            switcher.toggleVis(layer.get('name'));
        }
        var btLegend = document.createElement('button');
        btLegend.legendSwitch = layer.get('name');
        btLegend.innerHTML = '△';
        btLegend.className = 'c';
        btLegend.title = 'Toggle the legend visibility for the ' + layer.get('name') + ' layer.';
        btLegend.id = 'layerSwitcherBtLegend' + layer.get('name');
        btLegend.onclick = function (e) {
            switcher.toggleLegendVis(layer.get('name'), false);
        }
        title.append(btLegend);
        title.append(btHide);
        layerBlock.append(title);
        var itemsList = document.createElement('ul');
        itemsList.showLegend = true;
        itemsList.id = 'legend' + layer.get('name');
        if (layer instanceof ol.layer.Tile) {
            // Warning: hard coded stuff, if we want legends on other raster layers, this needs to change
            for (var i in genChunksPalette) {
                var curId = 'icon' + layer.get('name').replace(/ /, '') + i.replace(/\./, 'd');
                var row = document.createElement('li');
                var symbol = document.createElement('object');
                symbol.data = icons[layer.get('name')]
                symbol.style = 'width: 5mm; height: auto; vertical-align: middle; margin-right: 4pt; margin-bottom: 4pt;';
                symbol.type = 'image/svg+xml';
                symbol.id = curId;
                symbol.layer = layer.get('name');
                symbol.versionString = i;
                symbol.addEventListener('load', function (e) {
                    e.target.contentDocument.getElementById('icon').setAttribute('style', e.target.contentDocument.getElementById('icon').getAttribute('style').replace(/#ffffff/, 'rgb(' +
                            genChunksPalette[e.target.versionString][0] +
                            ',' +
                            genChunksPalette[e.target.versionString][1] +
                            ',' +
                            genChunksPalette[e.target.versionString][2] + ')'
                        )
                    );
                });
                row.append(symbol);
                row.append(document.createTextNode(i));
                itemsList.append(row);
            }
        } else {
            for (var i in colorsRef[layer.get('name')]) {
                var curId = 'icon' + layer.get('name') + i.replace(/ /, '');
                var row = document.createElement('li');
                var symbol = document.createElement('object');
                if (typeof (icons[layer.get('name')]) == "object") {
                    symbol.data = icons[layer.get('name')][i]
                    if (symbol.data.endsWith('png')) {
                        symbol.style = 'vertical-align: middle; margin-right: 4pt; margin-bottom: 4pt;';
                        symbol.type = 'image/png';
                    } else {
                        symbol.style = 'width: 5mm; height: auto; vertical-align: middle; margin-right: 4pt; margin-bottom: 4pt;';
                        symbol.type = 'image/svg+xml';
                    }
                } else {
                    symbol.data = icons[layer.get('name')]
                    symbol.style = 'width: 5mm; height: auto; vertical-align: middle; margin-right: 4pt; margin-bottom: 4pt;';
                    symbol.type = 'image/svg+xml';
                }
                symbol.id = curId;
                symbol.layer = layer.get('name');
                symbol.traderType = i;
                if (symbol.data.endsWith('svg')) {
                    symbol.addEventListener('load', function (e) {
                        e.target.contentDocument.getElementById('icon').setAttribute('style', e.target.contentDocument.getElementById('icon').getAttribute('style').replace(/#ffffff/, 'rgb(' +
                                colorsRef[e.target.layer][e.target.traderType][0] +
                                ',' +
                                colorsRef[e.target.layer][e.target.traderType][1] +
                                ',' +
                                colorsRef[e.target.layer][e.target.traderType][2] + ')'
                            )
                        );
                    });
                }
                row.append(symbol);
                row.append(document.createTextNode(i));
                itemsList.append(row);
                //svg.contentDocument.getElementById('icon').setAttribute('fill', i[1]);
            }
        }
        this.el.append(itemsList);
        this.layers[layer.get('name')] = layerBlock
    }
}

const switcher = new LayerSwitcher('layerSwitcher')

/* ######################### Highlight styles ######################### */
const highlightStyleTranslocator = [
    new ol.style.Style({
        stroke: new ol.style.Stroke({
            color: '#ddaaff',
            width: 3,
        }),
    }),
    new ol.style.Style({
        image: new ol.style.Icon({
            color: [255, 192, 255],
            opacity: 1,
            src: icons['Translocators']
        }),
        geometry: function (feature) {
            var coordinates = feature.getGeometry().getCoordinates();
            return new ol.geom.MultiPoint(coordinates);
        }
    })
];

const highlightStyleTrader = function (feature) {
    let wares = feature.get('wares');
    let color = colorsRef['Traders'][wares] || colorsRef['Traders']['unknown'];
    return new ol.style.Style({
        image: new ol.style.Icon({
            color: color.map((val, i) => Math.min(Math.max(val * 1.5, 64), 255)),
            src: icons['Traders'],
        })
    })
}

/* ######################### Default layer styles ######################### */
const vsLandmarks = new ol.layer.Vector({
    name: 'Landmarks',
    minZoom: 2,
    source: new ol.source.Vector({
        url: dataFolder + '/geojson/landmarks.geojson',
        format: new ol.format.GeoJSON(),
    }),
    style: function (feature) {
        let color = feature.get('color');
        let icon = feature.get('icon');
        let label = feature.get('label');
        return new ol.style.Style({
            zIndex: 1000,
            image: new ol.style.Icon({
                color: color,
                opacity: 1,
                src: `icons/${icon}.svg`,
            }),
            text: new ol.style.Text({
                font: 'bold ' + String(localStorage.labelSize) + 'px "arial narrow", "sans serif"',
                text: label,
                textAlign: 'left',
                textBaseline: 'bottom',
                offsetX: 10,
                fill: new ol.style.Fill({color: [0, 0, 0]}),
                stroke: new ol.style.Stroke({color: [255, 255, 255], width: 3}),
            })
        })
    }
});

const vsTraders = new ol.layer.Vector({
    name: 'Traders',
    minZoom: 3,
    source: new ol.source.Vector({
        url: dataFolder + '/geojson/traders.geojson',
        format: new ol.format.GeoJSON(),
    }),
    style: function (feature) {
        let wares = feature.get('wares');
        return new ol.style.Style({
            image: new ol.style.Icon({
                color: colorsRef['Traders'][wares],
                opacity: 1,
                src: icons['Traders'],
            }),
        })
    }
});

const vsTranslocators = new ol.layer.Vector({
    name: 'Translocators',
    minZoom: 3,
    source: new ol.source.Vector({
        url: dataFolder + '/geojson/teleporters.geojson',
        format: new ol.format.GeoJSON(),
    }),
    style: function (feature) {
        let color = feature.get('color')
        return [
            new ol.style.Style({
                stroke: new ol.style.Stroke({
                    color: tlCol.concat(0.5),
                    width: 2
                })
            }),
            new ol.style.Style({
                image: new ol.style.Icon({
                    color: color,
                    opacity: 1,
                    src: icons['Translocators']
                }),
                geometry: function (feature) {
                    var coordinates = feature.getGeometry().getCoordinates();
                    return new ol.geom.MultiPoint(coordinates);
                }
            })
        ];
    }
});

const vsWorld = new ol.layer.Tile({
    name: 'World',
    source: new ol.source.XYZ({
        interpolate: false,
        wrapx: false,
        tileGrid: new ol.tilegrid.TileGrid({
            origin: [-255.5, 255.5],
            resolutions: [16, 8, 4, 2, 1],
            tileSize: [256, 256]
        }),
        url: dataFolder + '/world/{z}_{x}_{y}.png'
    })
})

/* ######################### Controllers ######################### */
const mousePos = new ol.control.MousePosition({
    coordinateFormat: function (coordinate) {
        const relCoord = absoluteToRelative(coordinate)
        return ol.coordinate.toStringXY([relCoord[0], relCoord[1]], 0);
    },
    className: 'coords',
    target: document.getElementById('mousePos'),
    undefinedHTML: document.getElementById('mousePos').innerHTML
});

/* ######################### Map definition and functions ######################### */
const view = new ol.View({
    center: [cx, cy],
    constrainResolution: true,
    zoom: zm,
    resolutions: [32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125, 0.0625]
});

const map = new ol.Map({
    target: 'map',
    controls: [mousePos],
    layers: [
        vsWorld,
        vsTraders,
        vsTranslocators,
        vsLandmarks
    ],
    view: view
});

// map.on("moveend", function () {
//     newHref = adr + "?x=" + Math.round(map.getView().getCenter()[0]) + "&y=" + Math.round(map.getView().getCenter()[1]) + "&zoom=" + map.getView().getZoom();
//     window.history.pushState("pos", title, newHref);
// });


var selectedTL = undefined;
var selectedTrader = undefined;

// Feed the inspector
// The inspector itself should be a class/object with helper functions, would be much cleaner.
var desc = ''
map.on('pointermove', function (e) {
    if (selectedTL !== undefined) {
        selectedTL.setStyle(undefined);
        selectedTL = undefined;
    }
    if (selectedTrader !== undefined) {
        selectedTrader.setStyle(undefined);
        selectedTrader = undefined;
    }
    descTL = ''
    map.forEachFeatureAtPixel(e.pixel, function (f, l) {
        if (l.get('name') === 'Translocators') {
            selectedTL = f;
            descTL = f.get('label')
            if (descTL) {
                descTL = 'Translocator: ' + descTL;
            }
            f.setStyle(highlightStyleTranslocator);
            return true
        }
    });
    descTrader = ''
    map.forEachFeatureAtPixel(e.pixel, function (f, l) {
        if (l.get('name') === 'Traders') {
            selectedTrader = f;
            descTrader = 'Trader name: ' + f.get('name') + '<br>Wares: ' + f.get('wares');
            f.setStyle(highlightStyleTrader);
            return true
        }
        /*else if (l.get('name') == 'Landmarks') {
          desc = f.get('label');
        }*/
    });
    if (descTL || descTrader) {
        desc = ''
        if (descTL) {
            desc = descTL;
        }
        if (descTrader && descTL) {
            desc += '<br>';
        }
        if (descTrader) {
            desc += descTrader;
        }
        inspector.innerHTML = desc;
        inspector.style.display = 'block';
    } else {
        inspector.style.display = 'none';
    }
});

// Handle map clicks to display popups and other extended functionalities
map.on('singleclick', function (event) {
    map.forEachFeatureAtPixel(event.pixel, function (feature, layer) {
        if (layer.get('name') === 'Translocators') {
            let absCoords = feature.getGeometry().flatCoordinates;
            const coords = absoluteToRelative(absCoords);
            let dst1 = Math.abs(coords[0] - event.coordinate[0] + coords[1] - event.coordinate[1]);
            let dst2 = Math.abs(coords[2] - event.coordinate[0] + coords[3] - event.coordinate[1]);
            let coordsString = '';
            let coordsDst1 = (coords[0]) + ' 110 ' + (coords[1]);
            let coordsDst2 = (coords[2]) + ' 110 ' + (coords[3]);
            if (dst1 < dst2) {
                coordsString += coordsDst1;
                coordsString2 = coordsDst2;
            } else {
                coordsString += coordsDst2;
                coordsString2 = coordsDst1;
            }
            // Was the user holding shift?
            if (event.originalEvent.shiftKey) {
                // Zoom to the other end
                goToCoords(coordsString2.replace(/ 110 /, ','));
            } else {
                // Display popup
                let elements = {
                    'description1': {
                        'type': 'p',
                        'content': 'To add this translocator pair to your in game map, copy paste these two lines into the game chat. The translocator that is the closest to where you clicked on the line is first in the list.'
                    },
                    'description2': {
                        'type': 'p',
                        'content': `/waypoint addati spiral ${coordsString} false purple TL to ${coordsString2.replace(' 110 ', ', ')}`
                    },
                    'description3': {
                        'type': 'p',
                        'content': `/waypoint addati spiral ${coordsString2} false purple TL to ${coordsString.replace(' 110 ', ', ')}`
                    }
                }
                poper.createPopup('translocator', show = true, params = {'elements': elements});
            }
            return true
        } else if (layer.get('name') === 'Traders') {
            const absCoords = feature.getGeometry().flatCoordinates;
            const coords = absoluteToRelative(absCoords); 
            let color = '#' + colorsRef['Traders'][feature.get('wares')].map(i => i.toString(16).padStart(2, "0")).join("");
            var elements = {
                'description1': {
                    'type': 'p',
                    'content': 'To add this trader to your in game map, copy paste these the lines below into the game chat.'
                },
                'description2': {
                    'type': 'p',
                    'content': `/waypoint addati trader ${coords[0]} 110 ${coords[1]} false ${color.toUpperCase()} ${feature.get('name')} the ${feature.get('wares').toLowerCase()} trader`
                }
            }
            poper.createPopup('trader', show = true, params = {'elements': elements});
            return true
        }
    });
});

/* ######################### Build the legend (must come after the map definition)  ######################### */
switcher.buildLegend(vsTranslocators);
switcher.buildLegend(vsTraders);
switcher.buildLegend(vsLandmarks);

/* ######################### Start the popups and tools managers ######################### */
poper = new PopupManager();
tools = new Tools();
tools.addTools();

/* ######################### Key bindings ######################### */
window.onkeyup = function (kp) {
    var actions = {
        /*  G  */ 71: function () {
            poper.createPopup('gps');
        },
        /*  H  */ 72: function () {
            goToCoords('0,0');
        },
        /*  L  */ 76: function () {
            poper.createPopup('landmarks');
        },
        /* ESC */ 27: function () {
            poper.destroyPopup(Object.keys(poper.popups).pop());
        }
    }
    // Act on keypress if no popups are open
    if (Object.keys(poper.popups).length === 0 && kp.keyCode in actions) {
        actions[kp.keyCode]();
    }
    // Exception to allow closing popups with ESC
    else if (Object.keys(poper.popups).length > 0 && kp.keyCode in actions && kp.keyCode === 27) {
        actions[kp.keyCode]();
    }
}
