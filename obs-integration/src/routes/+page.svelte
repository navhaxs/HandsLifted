<script>
	const exampleSocket = new WebSocket('ws://localhost:8787/');
	
</script>

<div>
	<h1>Oh Happy Day, Happy Day</h1>
	<h1>You Washed My Sin Away</h1>
</div>

<style lang="scss">
	@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@600&display=swap');
	div {
		font-family: 'Montserrat', sans-serif;
		position: absolute;
		bottom: 0;
		left: 0;
		right: 0;
		color: white;

		background: rgb(26, 121, 164);
		background: linear-gradient(
			90deg,
			rgba(26, 121, 164, 0.8) 0%,
			rgba(35, 163, 220, 0.8) 50%,
			rgba(93, 181, 218, 0.8) 100%
		);

		height: 21vh;
		text-align: center;

		display: flex;
		justify-content: center;
		vertical-align: baseline;

		flex-direction: column;

		padding: 20px 0;

		h1 {
			font-size: 2.4vw;
			vertical-align: middle;
			margin: 0 auto;
		}

		h1:not(:last-child) {
			margin-bottom: 0.4em;
		}
	}
</style>
