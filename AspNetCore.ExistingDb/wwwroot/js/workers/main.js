function BruteForce(w, d, divContainerName, libsToLoad, workerCount, updateRate, alphabet, hashToCrack, passCharacterLength,
	foundAction) {

	var passphraseLimit = Math.pow(alphabet.length/*10 digits*/, passCharacterLength/*number of digits*/),
		workers = [], startTime = null;

	function onError(e) {
		updateTextContent('#pParagraph', [
			'ERROR: Line ', e.lineno, ' in ', e.filename, ': ', e.message
		].join(''));
	}

	function toArray(obj) {
		return [].map.call(obj, function (element) {
			return element;
		});
	}

	function splitNumIntoRanges(num, count) {
		var inc = Math.ceil(num / count),
			seq = 1,
			chunks = [];

		for (var i = 1; i < count; i++) {
			chunks.push({
				low: seq,
				high: seq + inc
			});

			seq += inc;
		}

		chunks.push({
			low: seq,
			high: num + 1
		});

		return chunks;
	}

	function createWorkerMonitor(index) {
		var element = d.createElement('section');
		element.id = 'worker-' + index;
		element.classList.add('worker');

		element.innerHTML =
			'<header>' +
			'	<span class="title">Worker ' + index + '</span>' +
			'</header>' +
			'<div class="data-element passphrase">' +
			'	<span class="label" style="color: black">Passphrase</span>' +
			'	<span class="value"/>' +
			'</div>' +
			'<div class="data-element hash">' +
			'	<span class="label" style="color: black">Hash</span>' +
			'	<span class="value"/>' +
			'</div>' +
			'<div class="data-element remaining">' +
			'	<span class="label" style="color: black">Combinations left</span>' +
			'	<span class="value"/>' +
			'</div>';

		d.querySelector(divContainerName).appendChild(element);
	}

	function updateTextContent(selector, content) {
		d.querySelector(selector).textContent = content;
	}

	this.run = function () {
		updateTextContent('.global-message', 'Starting ' + workerCount +
			' workers to brute force the SHA256 hash ' + hashToCrack + '.');

		// Splitting the limit number into pieces and distribute equally along the
		// workers.
		splitNumIntoRanges(passphraseLimit, workerCount).forEach(function (range, index) {
			var worker = new Worker('../js/workers/worker.min.js');
			createWorkerMonitor(index);

			worker.addEventListener('message', function (e) {
				var data = JSON.parse(arrayBufferToBinaryString(e.data));

				if (data.update) {
					// On a update we update the data of the specific worker
					updateTextContent('#worker-' + index + ' > .passphrase > .value', data.update.passphrase);
					updateTextContent('#worker-' + index + ' > .hash > .value', data.update.hash);
					updateTextContent('#worker-' + index + ' > .remaining > .value', data.update.remaining);
				}
				else if (data.found) {
					// If a worker found the correct hash we will set the global message and
					// terminate all workers.
					updateTextContent('#worker-' + index + ' > .passphrase > .value', data.found.passphrase);
					updateTextContent('#worker-' + index + ' > .hash > .value', data.found.hash);
					updateTextContent('#worker-' + index + ' > .remaining > .value', data.found.remaining);

					d.querySelector('#worker-' + index).classList.add('found');
					updateTextContent('.global-message', 'Worker ' + index +
						' found the passphrase ' + data.found.passphrase + ' within ' +
						(new Date().getTime() - startTime.getTime()) + 'ms!');

					// Terminate all workers
					workers.forEach(function (w) {
						// Worker.terminate() to interrupt the web worker
						w.terminate();
						// Add done class to all worker elements
						toArray(d.querySelectorAll('.worker')).forEach(function (e) {
							e.classList.add('done');
						});
					});
					workers = [];

					if(foundAction!=null && typeof(foundAction)!= undefined)
					{
						foundAction(data.found.passphrase);
					}
				}
				else if (data.done) {
					// If a worker is done before we found a result lets update the data and
					// style.
					updateTextContent('#worker-' + index + ' > .passphrase > .value', data.done.passphrase);
					updateTextContent('#worker-' + index + ' > .hash > .value', data.done.hash);
					updateTextContent('#worker-' + index + ' > .remaining > .value', '0');
					d.querySelector('#worker-' + index).classList.add('done');
				}
			});
			worker.addEventListener('error', onError, false);

			// Start the worker with a postMessage and pass the parameters
			var cmd = {
				libsToLoad: libsToLoad,
				hash: hashToCrack,
				range: range,
				alphabet: alphabet,
				passCharacterLength: passCharacterLength,
				updateRate: updateRate
			};
			var buff = binaryStringToArrayBuffer(JSON.stringify(cmd));
			worker.postMessage(buff, [buff]);

			// Push into the global workers array so we have controll later on
			workers.push(worker);
		});

		startTime = new Date();
	}

	this.clear = function () {
		startTime = null;

		workers.forEach(function (w) {
			// Worker.terminate() to interrupt the web worker
			w.terminate();
		});

		workers = [];

		toArray(d.querySelectorAll('.worker')).forEach(function (node) {
			node.parentNode.removeChild(node);
		});

		updateTextContent('.global-message', '');
	}
}