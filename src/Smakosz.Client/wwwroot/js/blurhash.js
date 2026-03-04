// Based on https://github.com/woltapp/blurhash (MIT License)
window.blurhashInterop = {
    decode: function (canvasId, hash, width, height) {
        try {
            var canvas = document.getElementById(canvasId);
            if (!canvas || !hash) return;
            var pixels = decodeBlurhash(hash, width, height);
            if (!pixels) return;
            var ctx = canvas.getContext('2d');
            var imageData = ctx.createImageData(width, height);
            imageData.data.set(pixels);
            ctx.putImageData(imageData, 0, 0);
        } catch (e) { /* silently fail - show placeholder */ }
    },
    preloadImage: function (url) {
        return new Promise(function (resolve) {
            var img = new Image();
            img.onload = function () { resolve(true); };
            img.onerror = function () { resolve(false); };
            img.src = url;
        });
    }
};

function decodeBlurhash(blurhash, width, height) {
    if (!blurhash || blurhash.length < 6) return null;

    var sizeFlag = decode83(blurhash[0]);
    var numY = Math.floor(sizeFlag / 9) + 1;
    var numX = (sizeFlag % 9) + 1;
    var expectedLength = 4 + 2 * numX * numY;
    if (blurhash.length !== expectedLength) return null;

    var quantisedMaximumValue = decode83(blurhash[1]);
    var maximumValue = (quantisedMaximumValue + 1) / 166;

    var colors = new Array(numX * numY);
    for (var i = 0; i < colors.length; i++) {
        if (i === 0) {
            var value = decode83(blurhash.substring(2, 6));
            colors[i] = decodeDC(value);
        } else {
            var value = decode83(blurhash.substring(4 + i * 2, 6 + i * 2));
            colors[i] = decodeAC(value, maximumValue);
        }
    }

    var pixels = new Uint8ClampedArray(width * height * 4);
    for (var y = 0; y < height; y++) {
        for (var x = 0; x < width; x++) {
            var r = 0, g = 0, b = 0;
            for (var j = 0; j < numY; j++) {
                for (var i = 0; i < numX; i++) {
                    var basis = Math.cos((Math.PI * x * i) / width) * Math.cos((Math.PI * y * j) / height);
                    var color = colors[i + j * numX];
                    r += color[0] * basis;
                    g += color[1] * basis;
                    b += color[2] * basis;
                }
            }
            var idx = 4 * (x + y * width);
            pixels[idx] = linearTosRGB(r);
            pixels[idx + 1] = linearTosRGB(g);
            pixels[idx + 2] = linearTosRGB(b);
            pixels[idx + 3] = 255;
        }
    }
    return pixels;
}

var digitChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-.:;=?@[]^_{|}~";

function decode83(str) {
    var value = 0;
    for (var i = 0; i < str.length; i++) {
        var c = digitChars.indexOf(str[i]);
        if (c === -1) return 0;
        value = value * 83 + c;
    }
    return value;
}

function sRGBToLinear(value) {
    var v = value / 255;
    return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
}

function linearTosRGB(value) {
    var v = Math.max(0, Math.min(1, value));
    return v <= 0.0031308 ? Math.round(v * 12.92 * 255 + 0.5) : Math.round((1.055 * Math.pow(v, 1 / 2.4) - 0.055) * 255 + 0.5);
}

function signPow(val, exp) {
    return (val < 0 ? -1 : 1) * Math.pow(Math.abs(val), exp);
}

function decodeDC(value) {
    var r = value >> 16;
    var g = (value >> 8) & 255;
    var b = value & 255;
    return [sRGBToLinear(r), sRGBToLinear(g), sRGBToLinear(b)];
}

function decodeAC(value, maximumValue) {
    var quantR = Math.floor(value / (19 * 19));
    var quantG = Math.floor(value / 19) % 19;
    var quantB = value % 19;
    return [
        signPow((quantR - 9) / 9, 2.0) * maximumValue,
        signPow((quantG - 9) / 9, 2.0) * maximumValue,
        signPow((quantB - 9) / 9, 2.0) * maximumValue
    ];
}
